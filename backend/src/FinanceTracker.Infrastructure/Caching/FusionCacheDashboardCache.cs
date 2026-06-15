using FinanceTracker.Application.Common.Interfaces;
using ZiggyCreatures.Caching.Fusion;

namespace FinanceTracker.Infrastructure.Caching;

/// <summary>
/// <see cref="IDashboardCache"/> backed by FusionCache (in-memory + Redis
/// backplane). Each entry is tagged <c>dashboard:{userId}</c> so a single
/// <see cref="InvalidateAsync"/> drops every dashboard aggregate for that user
/// when their accruals change (T2.1.5). Cache stampede protection and fail-safe
/// are provided by FusionCache's default entry options (see DI registration).
/// </summary>
public sealed class FusionCacheDashboardCache(IFusionCache cache) : IDashboardCache
{
    private static string TagFor(Guid userId) => $"dashboard:{userId}";

    public Task<T> GetOrSetAsync<T>(
        string key,
        Guid userId,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default) =>
        cache.GetOrSetAsync<T>(
            key,
            (_, ct) => factory(ct),
            tags: [TagFor(userId)],
            token: cancellationToken)
            .AsTask();

    public Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default) =>
        cache.RemoveByTagAsync(TagFor(userId), token: cancellationToken).AsTask();
}
