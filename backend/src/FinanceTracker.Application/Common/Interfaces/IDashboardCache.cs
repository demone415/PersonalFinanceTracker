namespace FinanceTracker.Application.Common.Interfaces;

/// <summary>
/// Per-user cache for dashboard aggregates (T2.1.5). Keeps the Application layer
/// independent of the concrete cache (FusionCache + Redis backplane is wired in
/// Infrastructure). Entries are tagged per user so a single
/// <see cref="InvalidateAsync"/> call drops every cached aggregate for that user
/// when their accruals change. Cache keys must include the UserId so aggregates
/// never leak between users (ARCHITECTURE.md §11.3).
/// </summary>
public interface IDashboardCache
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/> or invokes
    /// <paramref name="factory"/> and caches its result, tagged for
    /// <paramref name="userId"/> so it can be invalidated as a group.
    /// </summary>
    Task<T> GetOrSetAsync<T>(
        string key,
        Guid userId,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default);

    /// <summary>Drops every cached dashboard aggregate for the given user.</summary>
    Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default);
}
