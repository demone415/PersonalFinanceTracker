namespace FinanceTracker.Application.Common.Interfaces;

/// <summary>
/// Why a receipt fetch may or may not call the provider right now (ARCHITECTURE.md
/// §4 / §11.4). A daily global cap protects the shared 15/day provider quota; a
/// per-user cap stops one user monopolising it; and an unreachable limiter store
/// fails <em>closed</em> — losing the counter must never risk exceeding the
/// provider's limit and getting the key banned.
/// </summary>
public enum RateLimitDecision
{
    /// <summary>A slot was acquired against both the global and per-user caps; the call may proceed.</summary>
    Allowed,

    /// <summary>The global daily quota (e.g. 15/day) is exhausted; defer until tomorrow.</summary>
    GlobalQuotaExceeded,

    /// <summary>This user's daily cap is exhausted; defer to give other users a turn.</summary>
    UserQuotaExceeded,

    /// <summary>The limiter store (Redis) is unavailable — fail-closed; pause the queue.</summary>
    LimiterUnavailable,
}

/// <summary>
/// Guards calls to the external receipt provider against the shared daily quota.
/// Backed by Redis counters with end-of-day TTL; see the infrastructure
/// implementation for the fail-closed behaviour.
/// </summary>
public interface IReceiptRateLimiter
{
    /// <summary>
    /// Atomically tries to consume one daily slot for <paramref name="userId"/>.
    /// On any non-<see cref="RateLimitDecision.Allowed"/> result no quota is spent.
    /// </summary>
    Task<RateLimitDecision> TryAcquireAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Remaining global slots for the current UTC day, or <c>null</c> if the limiter is unavailable.</summary>
    Task<int?> GetRemainingGlobalQuotaAsync(CancellationToken ct = default);
}
