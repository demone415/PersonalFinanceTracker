using FinanceTracker.Application.Common.Interfaces;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// Default <see cref="IUnitOfWork"/> backed by <see cref="AppDbContext"/>.
/// Registered per request (scoped) so it shares the request's change tracker.
/// </summary>
public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
