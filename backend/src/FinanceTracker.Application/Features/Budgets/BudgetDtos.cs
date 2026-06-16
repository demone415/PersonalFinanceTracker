namespace FinanceTracker.Application.Features.Budgets;

/// <summary>A monthly budget as returned to clients, enriched with its category's display fields.</summary>
public sealed record BudgetDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string CategoryColor,
    string CategoryIcon,
    int Year,
    int Month,
    decimal LimitAmount,
    string Currency);

/// <summary>Payload for creating a budget.</summary>
public sealed record CreateBudgetRequest(
    Guid CategoryId,
    int Year,
    int Month,
    decimal LimitAmount,
    string Currency);

/// <summary>Payload for updating a budget; only the limit and currency are mutable.</summary>
public sealed record UpdateBudgetRequest(
    decimal LimitAmount,
    string Currency);

/// <summary>
/// Spend progress for one budget in a given month (T5.1.3). <see cref="SpentAmount"/>
/// is the net category expense; <see cref="Percentage"/> is spent/limit×100 and may
/// exceed 100 when the budget is overrun.
/// </summary>
public sealed record BudgetProgressDto(
    Guid BudgetId,
    Guid CategoryId,
    string CategoryName,
    string CategoryColor,
    string CategoryIcon,
    int Year,
    int Month,
    decimal LimitAmount,
    decimal SpentAmount,
    decimal RemainingAmount,
    double Percentage,
    string Currency);
