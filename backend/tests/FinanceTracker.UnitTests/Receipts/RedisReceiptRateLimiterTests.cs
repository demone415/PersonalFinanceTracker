using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Infrastructure.ExternalProviders.ProverkaChecka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace FinanceTracker.UnitTests.Receipts;

/// <summary>
/// Daily-quota rate limiter (T4.1.3/T4.1.6): global + per-user caps via Redis
/// INCR, and the fail-closed behaviour when Redis is unavailable (ARCHITECTURE.md
/// §11.4 — losing the counter must never exceed the provider's limit).
/// </summary>
public class RedisReceiptRateLimiterTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static readonly ProverkaCheckaOptions DefaultOptions = new()
    {
        Token = "t",
        DailyGlobalLimit = 15,
        PerUserDailyLimit = 5,
    };

    private static bool IsGlobalKey(RedisKey key) => key.ToString().StartsWith("receipt:daily-count");
    private static bool IsUserKey(RedisKey key) => key.ToString().StartsWith("receipt:user-count");

    private static RedisReceiptRateLimiter BuildLimiter(IDatabase db)
    {
        var mux = new Mock<IConnectionMultiplexer>();
        mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db);
        return new RedisReceiptRateLimiter(
            mux.Object, Options.Create(DefaultOptions), NullLogger<RedisReceiptRateLimiter>.Instance);
    }

    [Fact]
    public async Task TryAcquire_WithinBothCaps_IsAllowed()
    {
        var db = new Mock<IDatabase>();
        db.Setup(d => d.StringIncrementAsync(It.Is<RedisKey>(k => IsGlobalKey(k)), 1L, CommandFlags.None))
            .ReturnsAsync(5L);
        db.Setup(d => d.StringIncrementAsync(It.Is<RedisKey>(k => IsUserKey(k)), 1L, CommandFlags.None))
            .ReturnsAsync(2L);

        var decision = await BuildLimiter(db.Object).TryAcquireAsync(UserId);

        Assert.Equal(RateLimitDecision.Allowed, decision);
        db.Verify(d => d.StringDecrementAsync(It.IsAny<RedisKey>(), 1L, CommandFlags.None), Times.Never);
    }

    [Fact]
    public async Task TryAcquire_GlobalCapExceeded_ReleasesGlobalSlot()
    {
        var db = new Mock<IDatabase>();
        db.Setup(d => d.StringIncrementAsync(It.Is<RedisKey>(k => IsGlobalKey(k)), 1L, CommandFlags.None))
            .ReturnsAsync(16L); // over the 15/day cap
        db.Setup(d => d.StringDecrementAsync(It.IsAny<RedisKey>(), 1L, CommandFlags.None))
            .ReturnsAsync(15L);

        var decision = await BuildLimiter(db.Object).TryAcquireAsync(UserId);

        Assert.Equal(RateLimitDecision.GlobalQuotaExceeded, decision);
        db.Verify(d => d.StringDecrementAsync(It.Is<RedisKey>(k => IsGlobalKey(k)), 1L, CommandFlags.None), Times.Once);
        // The user counter must not have been touched once the global cap rejected the call.
        db.Verify(d => d.StringIncrementAsync(It.Is<RedisKey>(k => IsUserKey(k)), 1L, CommandFlags.None), Times.Never);
    }

    [Fact]
    public async Task TryAcquire_UserCapExceeded_ReleasesBothSlots()
    {
        var db = new Mock<IDatabase>();
        db.Setup(d => d.StringIncrementAsync(It.Is<RedisKey>(k => IsGlobalKey(k)), 1L, CommandFlags.None))
            .ReturnsAsync(5L);
        db.Setup(d => d.StringIncrementAsync(It.Is<RedisKey>(k => IsUserKey(k)), 1L, CommandFlags.None))
            .ReturnsAsync(6L); // over the per-user cap of 5
        db.Setup(d => d.StringDecrementAsync(It.IsAny<RedisKey>(), 1L, CommandFlags.None))
            .ReturnsAsync(0L);

        var decision = await BuildLimiter(db.Object).TryAcquireAsync(UserId);

        Assert.Equal(RateLimitDecision.UserQuotaExceeded, decision);
        db.Verify(d => d.StringDecrementAsync(It.Is<RedisKey>(k => IsUserKey(k)), 1L, CommandFlags.None), Times.Once);
        db.Verify(d => d.StringDecrementAsync(It.Is<RedisKey>(k => IsGlobalKey(k)), 1L, CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task TryAcquire_RedisUnavailable_FailsClosed()
    {
        var mux = new Mock<IConnectionMultiplexer>();
        mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));
        var limiter = new RedisReceiptRateLimiter(
            mux.Object, Options.Create(DefaultOptions), NullLogger<RedisReceiptRateLimiter>.Instance);

        var decision = await limiter.TryAcquireAsync(UserId);

        Assert.Equal(RateLimitDecision.LimiterUnavailable, decision);
    }

    [Fact]
    public async Task TryAcquire_FirstCallOfDay_SetsTtl()
    {
        var db = new Mock<IDatabase>();
        db.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), 1L, CommandFlags.None))
            .ReturnsAsync(1L); // first increment of the day for both keys
        db.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<DateTime?>(),
                It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var decision = await BuildLimiter(db.Object).TryAcquireAsync(UserId);

        Assert.Equal(RateLimitDecision.Allowed, decision);
        db.Verify(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<DateTime?>(),
            It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()), Times.Exactly(2));
    }
}
