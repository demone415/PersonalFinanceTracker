using FinanceTracker.Application.Features.Budgets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

/// <summary>CRUD and progress for monthly budgets (Epic 5). All endpoints require authentication.</summary>
[ApiController]
[Authorize]
[Route("api/v1/budgets")]
public sealed class BudgetsController : ControllerBase
{
    /// <summary>Lists the caller's budgets, optionally filtered by year/month.</summary>
    [HttpGet]
    public Task<IReadOnlyList<BudgetDto>> GetAll(
        [FromServices] BudgetService budgets,
        CancellationToken cancellationToken,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null) =>
        budgets.GetAllAsync(year, month, cancellationToken);

    /// <summary>Spend progress per category budget for a month (defaults to the current month).</summary>
    [HttpGet("progress")]
    public Task<IReadOnlyList<BudgetProgressDto>> GetProgress(
        [FromServices] BudgetService budgets,
        CancellationToken cancellationToken,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null) =>
        budgets.GetProgressAsync(year, month, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<BudgetDto> GetById(
        Guid id,
        [FromServices] BudgetService budgets,
        CancellationToken cancellationToken) =>
        budgets.GetByIdAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<BudgetDto>> Create(
        [FromBody] CreateBudgetRequest request,
        [FromServices] BudgetService budgets,
        CancellationToken cancellationToken)
    {
        var created = await budgets.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<BudgetDto> Update(
        Guid id,
        [FromBody] UpdateBudgetRequest request,
        [FromServices] BudgetService budgets,
        CancellationToken cancellationToken) =>
        budgets.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] BudgetService budgets,
        CancellationToken cancellationToken)
    {
        await budgets.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
