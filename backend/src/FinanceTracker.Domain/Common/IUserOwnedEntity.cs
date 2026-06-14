namespace FinanceTracker.Domain.Common;

/// <summary>
/// Marks an entity that is owned by a user and therefore subject to the EF Core
/// global query filter for data isolation (ARCHITECTURE.md §11.2):
/// <c>IsAdmin || UserId == currentUser</c>. Entities implementing this interface
/// (Accrual, Receipt, Category, MonthlyBudget, ChangeLog, BackgroundTask, …) are
/// filtered automatically — no manual <c>WHERE UserId</c> anywhere.
/// </summary>
public interface IUserOwnedEntity
{
    Guid UserId { get; }
}
