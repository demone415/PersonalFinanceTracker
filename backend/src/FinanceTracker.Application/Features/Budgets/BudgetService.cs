using FinanceTracker.Application.Common.Exceptions;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Application.Features.Budgets;

/// <summary>
/// Feature service for monthly budgets (Epic 5). Data isolation is enforced by
/// the EF Core global query filter; a user may hold at most one budget per
/// category and month. Progress (T5.1.3) is computed live from accruals — only
/// expenses with <c>IncludeInStats = true</c> count, net of returns.
/// </summary>
public sealed class BudgetService(
    IApplicationDbContext db,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
{
    public async Task<IReadOnlyList<BudgetDto>> GetAllAsync(
        int? year, int? month, CancellationToken cancellationToken = default)
    {
        var query = db.MonthlyBudgets.AsQueryable();

        // Apply the filter whenever a value is supplied — an out-of-range month
        // then yields an empty result rather than being silently ignored.
        if (year.HasValue)
            query = query.Where(b => b.Year == year.Value);
        if (month.HasValue)
            query = query.Where(b => b.Month == month.Value);

        return await query
            .OrderByDescending(b => b.Year)
            .ThenByDescending(b => b.Month)
            .ThenBy(b => b.Category!.Name)
            .Select(b => new BudgetDto(
                b.Id,
                b.CategoryId,
                b.Category!.Name,
                b.Category.Color,
                b.Category.Icon,
                b.Year,
                b.Month,
                b.LimitAmount,
                b.Currency))
            .ToListAsync(cancellationToken);
    }

    public async Task<BudgetDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.MonthlyBudgets
            .Where(b => b.Id == id)
            .Select(b => new BudgetDto(
                b.Id,
                b.CategoryId,
                b.Category!.Name,
                b.Category.Color,
                b.Category.Icon,
                b.Year,
                b.Month,
                b.LimitAmount,
                b.Currency))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(MonthlyBudget), id);

    public async Task<BudgetDto> CreateAsync(
        CreateBudgetRequest request, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("Authentication is required to create a budget.");

        // The category must exist and be visible to the caller (filter applies).
        var categoryExists = await db.Categories
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
            throw new NotFoundException(nameof(Category), request.CategoryId);

        // Scope by owner explicitly: the unique index is (UserId, CategoryId,
        // Year, Month), and an admin bypasses the global filter — without this an
        // admin would get a false conflict against another user's budget.
        var duplicate = await db.MonthlyBudgets.AnyAsync(
            b => b.UserId == userId
                 && b.CategoryId == request.CategoryId
                 && b.Year == request.Year
                 && b.Month == request.Month,
            cancellationToken);
        if (duplicate)
            throw new ConflictException("A budget for this category and month already exists.");

        var budget = new MonthlyBudget(
            userId, request.CategoryId, request.Year, request.Month, request.LimitAmount, request.Currency);

        db.MonthlyBudgets.Add(budget);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(budget.Id, cancellationToken);
    }

    public async Task<BudgetDto> UpdateAsync(
        Guid id, UpdateBudgetRequest request, CancellationToken cancellationToken = default)
    {
        var budget = await LoadOwnedAsync(id, cancellationToken);
        budget.Update(request.LimitAmount, request.Currency);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(budget.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var budget = await LoadOwnedAsync(id, cancellationToken);
        db.MonthlyBudgets.Remove(budget);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Spend progress for every budget in the given month (defaults to the current
    /// UTC month). One net-expense aggregate is computed in the database, then
    /// matched to the month's budgets in memory.
    /// </summary>
    public async Task<IReadOnlyList<BudgetProgressDto>> GetProgressAsync(
        int? year, int? month, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("Authentication is required to view budget progress.");
        var (y, m) = Normalize(year, month);

        // Scope to the caller explicitly: this endpoint reports "my" progress, and
        // an admin bypasses the global filter — without this both the budgets and
        // the spend aggregate below would mix every user's data into one figure.
        var budgets = await db.MonthlyBudgets
            .Where(b => b.UserId == userId && b.Year == y && b.Month == m)
            .Select(b => new
            {
                b.Id,
                b.CategoryId,
                CategoryName = b.Category!.Name,
                b.Category.Color,
                b.Category.Icon,
                b.Year,
                b.Month,
                b.LimitAmount,
                b.Currency,
            })
            .ToListAsync(cancellationToken);

        if (budgets.Count == 0)
            return [];

        var from = new DateTimeOffset(y, m, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1);
        var categoryIds = budgets.Select(b => b.CategoryId).ToList();

        // Amounts are aggregated as stored, i.e. in the user's base currency —
        // the same contract DashboardService relies on (Accrual.Currency /
        // ExchangeRate carry the original transaction values for reference only).
        var spentByCategory = await db.Accruals
            .Where(a => a.UserId == userId
                        && a.IncludeInStats
                        && a.Date >= from && a.Date < to
                        && a.CategoryId != null && categoryIds.Contains(a.CategoryId.Value)
                        && (a.Type == AccrualType.Expense || a.Type == AccrualType.ReturnExpense))
            .GroupBy(a => a.CategoryId!.Value)
            .Select(g => new
            {
                CategoryId = g.Key,
                Spent = g.Sum(x => x.Type == AccrualType.ReturnExpense ? -x.Amount : x.Amount),
            })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Spent, cancellationToken);

        return budgets
            .Select(b =>
            {
                var spent = spentByCategory.TryGetValue(b.CategoryId, out var s) ? s : 0m;
                var percentage = b.LimitAmount > 0m
                    ? (double)Math.Round(spent / b.LimitAmount * 100m, 1)
                    : 0d;
                return new BudgetProgressDto(
                    b.Id,
                    b.CategoryId,
                    b.CategoryName,
                    b.Color,
                    b.Icon,
                    b.Year,
                    b.Month,
                    b.LimitAmount,
                    spent,
                    b.LimitAmount - spent,
                    percentage,
                    b.Currency);
            })
            .OrderByDescending(p => p.Percentage)
            .ToList();
    }

    private async Task<MonthlyBudget> LoadOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        var budget = await db.MonthlyBudgets.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(MonthlyBudget), id);

        if (!currentUser.IsAdmin && budget.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not own this budget.");

        return budget;
    }

    private static (int Year, int Month) Normalize(int? year, int? month)
    {
        var now = DateTimeOffset.UtcNow;
        var y = year ?? now.Year;
        var m = month is >= 1 and <= 12 ? month.Value : now.Month;
        return (y, m);
    }
}
