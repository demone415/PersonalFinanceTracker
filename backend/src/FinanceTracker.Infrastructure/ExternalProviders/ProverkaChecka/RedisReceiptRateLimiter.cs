using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Infrastructure.ExternalProviders.ProverkaChecka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FinanceTracker.Infrastructure.ExternalProviders.ProverkaChecka;

/// <summary>
/// Daily-quota guard for the receipt provider, backed by Redis counters with an
/// end-of-UTC-day TTL (ARCHITECTURE.md §4 / §11.4). Enforces a shared global cap
/// (≈15/day) and a per-user cap, both via atomic <c>INCR</c>. If Redis is
/// unreachable it fails <em>closed</em> (<see cref="RateLimitDecision.LimiterUnavailable"/>):
/// losing the counter must never let us exceed the provider's limit and risk a ban.
/// </summary>
internal sealed class RedisReceiptRateLimiter(
    IConnectionMultiplexer redis,
    IOptions<ProverkaCheckaOptions> options,
    ILogger<RedisReceiptRateLimiter> logger) : IReceiptRateLimiter
{
    private readonly ProverkaCheckaOptions _options = options.Value;

    public async Task<RateLimitDecision> TryAcquireAsync(Guid userId, CancellationToken ct = default)
    {
        var day = DateTimeOffset.UtcNow;
        var expiry = EndOfUtcDay(day);
        var globalKey = GlobalKey(day);
        var userKey = UserKey(userId, day);

        try
        {
            var db = redis.GetDatabase();

            // Spend a global slot first, then a per-user slot; release on any breach
            // so a rejected attempt never permanently consumes quota.
            var global = await IncrementWithExpiryAsync(db, globalKey, expiry);
            if (global > _options.DailyGlobalLimit)
            {
                await db.StringDecrementAsync(globalKey);
                return RateLimitDecision.GlobalQuotaExceeded;
            }

            var user = await IncrementWithExpiryAsync(db, userKey, expiry);
            if (user > _options.PerUserDailyLimit)
            {
                await db.StringDecrementAsync(userKey);
                await db.StringDecrementAsync(globalKey);
                return RateLimitDecision.UserQuotaExceeded;
            }

            return RateLimitDecision.Allowed;
        }
        catch (RedisException ex)
        {
            // Fail-closed: pause processing rather than call the provider blindly.
            logger.LogError(ex, "Receipt rate limiter unavailable (Redis); failing closed.");
            return RateLimitDecision.LimiterUnavailable;
        }
        catch (TimeoutException ex)
        {
            logger.LogError(ex, "Receipt rate limiter timed out (Redis); failing closed.");
            return RateLimitDecision.LimiterUnavailable;
        }
    }

    public async Task<int?> GetRemainingGlobalQuotaAsync(CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var used = (int)await db.StringGetAsync(GlobalKey(DateTimeOffset.UtcNow));
            return Math.Max(0, _options.DailyGlobalLimit - used);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Could not read remaining provider quota from Redis.");
            return null;
        }
    }

    private static async Task<long> IncrementWithExpiryAsync(IDatabase db, RedisKey key, DateTimeOffset expiry)
    {
        var value = await db.StringIncrementAsync(key);
        if (value == 1)
        {
            // First increment of the day creates the key; pin its TTL to UTC midnight.
            await db.KeyExpireAsync(key, expiry.UtcDateTime, ExpireWhen.Always, CommandFlags.None);
        }

        return value;
    }

    private static string GlobalKey(DateTimeOffset day) => $"receipt:daily-count:{day:yyyy-MM-dd}";

    private static string UserKey(Guid userId, DateTimeOffset day) =>
        $"receipt:user-count:{userId:N}:{day:yyyy-MM-dd}";

    private static DateTimeOffset EndOfUtcDay(DateTimeOffset now) =>
        new(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
}
