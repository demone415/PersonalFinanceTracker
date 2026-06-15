namespace FinanceTracker.Application.Features.Dashboard;

/// <summary>
/// Headline figures for the dashboard (T2.1.1): all-time net balance plus the
/// income / expense / balance for a single month. Only accruals with
/// <c>IncludeInStats = true</c> are counted. Amounts are summed in their stored
/// currency; cross-currency conversion arrives with Epic 8 — until then a single
/// base currency is assumed.
/// </summary>
public sealed record DashboardSummaryDto(
    decimal TotalBalance,
    decimal MonthIncome,
    decimal MonthExpense,
    decimal MonthBalance,
    int Year,
    int Month);

/// <summary>One slice of the expenses-by-category pie chart (T2.1.2).</summary>
public sealed record ExpenseByCategoryDto(
    Guid? CategoryId,
    string CategoryName,
    string Color,
    string Icon,
    decimal Amount,
    double Percentage);

/// <summary>One point on the 6-month income/expense dynamics chart (T2.1.3).</summary>
public sealed record MonthlyDynamicsPointDto(
    int Year,
    int Month,
    decimal Income,
    decimal Expense);

/// <summary>One bar of the top-categories chart (T2.1.4).</summary>
public sealed record TopCategoryDto(
    Guid? CategoryId,
    string CategoryName,
    string Color,
    string Icon,
    decimal Amount);
