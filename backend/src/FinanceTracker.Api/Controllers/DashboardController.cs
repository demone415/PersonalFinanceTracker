using FinanceTracker.Application.Features.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

/// <summary>
/// Dashboard aggregates (Story 2.1). Figures are computed in the database and
/// cached per user for 5 minutes (FusionCache). All endpoints require auth and
/// are isolated by the EF Core global query filter.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public sealed class DashboardController : ControllerBase
{
    /// <summary>All-time balance plus income/expense/balance for a month (current by default).</summary>
    [HttpGet("summary")]
    public Task<DashboardSummaryDto> GetSummary(
        [FromServices] DashboardService dashboard,
        CancellationToken cancellationToken,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null) =>
        dashboard.GetSummaryAsync(year, month, cancellationToken);

    /// <summary>Expense totals grouped by category for the pie chart.</summary>
    [HttpGet("expenses-by-category")]
    public Task<IReadOnlyList<ExpenseByCategoryDto>> GetExpensesByCategory(
        [FromServices] DashboardService dashboard,
        CancellationToken cancellationToken,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null) =>
        dashboard.GetExpensesByCategoryAsync(year, month, cancellationToken);

    /// <summary>Income/expense per month over the last <paramref name="months"/> months.</summary>
    [HttpGet("monthly-dynamics")]
    public Task<IReadOnlyList<MonthlyDynamicsPointDto>> GetMonthlyDynamics(
        [FromServices] DashboardService dashboard,
        CancellationToken cancellationToken,
        [FromQuery] int months = 6) =>
        dashboard.GetMonthlyDynamicsAsync(months, cancellationToken);

    /// <summary>Top expense categories for the month (current by default).</summary>
    [HttpGet("top-categories")]
    public Task<IReadOnlyList<TopCategoryDto>> GetTopCategories(
        [FromServices] DashboardService dashboard,
        CancellationToken cancellationToken,
        [FromQuery] int limit = 5,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null) =>
        dashboard.GetTopCategoriesAsync(limit, year, month, cancellationToken);
}
