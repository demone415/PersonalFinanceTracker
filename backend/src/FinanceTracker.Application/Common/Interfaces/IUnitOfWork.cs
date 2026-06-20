namespace FinanceTracker.Application.Common.Interfaces;

/// <summary>
/// Coordinates persistence of all changes made within a single business operation.
/// Application services depend on this abstraction and never call
/// <c>DbContext.SaveChanges()</c> directly.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Persists all tracked changes to the underlying store.</summary>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops all changes tracked since the last save (detaches tracked entities)
    /// without touching the store. Used to abandon a half-built unit of work after
    /// a non-DB failure, so a subsequent <see cref="SaveChangesAsync"/> on the same
    /// scope can't accidentally commit a partial result.
    /// </summary>
    void DiscardChanges();
}
