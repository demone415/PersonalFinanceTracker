using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Entities;

/// <summary>
/// A spending limit set by a user for one category in a single calendar month
/// (Epic 5). Identity is the tuple (<see cref="UserId"/>, <see cref="CategoryId"/>,
/// <see cref="Year"/>, <see cref="Month"/>); only the limit and currency are
/// mutable. Data isolation is enforced by the EF Core global query filter via
/// <see cref="IUserOwnedEntity"/>.
/// </summary>
public class MonthlyBudget : IUserOwnedEntity
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid CategoryId { get; private set; }

    /// <summary>Four-digit calendar year, e.g. 2026.</summary>
    public int Year { get; private set; }

    /// <summary>Calendar month, 1–12.</summary>
    public int Month { get; private set; }

    /// <summary>The spending ceiling for the category in this month.</summary>
    public decimal LimitAmount { get; private set; }

    public string Currency { get; private set; }

    // Navigation
    public Category? Category { get; private set; }

    private MonthlyBudget() { Currency = "RUB"; }

    /// <summary>Creates a budget with an app-generated GUID v7 id.</summary>
    public MonthlyBudget(
        Guid userId,
        Guid categoryId,
        int year,
        int month,
        decimal limitAmount,
        string currency = "RUB")
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        CategoryId = categoryId;
        Year = year;
        Month = month;
        LimitAmount = limitAmount;
        Currency = currency;
    }

    /// <summary>Updates the mutable fields; the category and period are fixed for the budget's lifetime.</summary>
    public void Update(decimal limitAmount, string currency)
    {
        LimitAmount = limitAmount;
        Currency = currency;
    }
}
