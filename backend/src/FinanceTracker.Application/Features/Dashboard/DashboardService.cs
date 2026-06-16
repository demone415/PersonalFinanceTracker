using FinanceTracker.Application.Common.Exceptions;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Application.Features.Dashboard;

/// <summary>
/// Dashboard aggregates (Story 2.1). All figures are computed in the database
/// with <c>GROUP BY</c> (ARCHITECTURE.md §11.3) and cached per user for 5 minutes
/// via <see cref="IDashboardCache"/>. Data isolation is enforced by the EF Core
/// global query filter; admins see system-wide aggregates. Only accruals with
/// <c>IncludeInStats = true</c> are counted.
/// </summary>
public sealed class DashboardService(
    IApplicationDbContext db,
    IDashboardCache cache,
    ICurrentUserService currentUser)
{
    // Fallback presentation for accruals with no category.
    private const string UncategorizedName = "Без категории";
    private const string UncategorizedColor = "#6B7280";
    private const string UncategorizedIcon = "ellipsis";

    public Task<DashboardSummaryDto> GetSummaryAsync(
        int? year, int? month, CancellationToken cancellationToken = default)
    {
        var userId = RequireUser();
        var (y, m) = Normalize(year, month);

        return cache.GetOrSetAsync(
            $"dashboard:summary:{userId}:{y}-{m}",
            userId,
            ct => ComputeSummaryAsync(y, m, ct),
            cancellationToken);
    }

    public Task<IReadOnlyList<ExpenseByCategoryDto>> GetExpensesByCategoryAsync(
        int? year, int? month, CancellationToken cancellationToken = default)
    {
        var userId = RequireUser();
        var (y, m) = Normalize(year, month);

        return cache.GetOrSetAsync(
            $"dashboard:expenses-by-category:{userId}:{y}-{m}",
            userId,
            ct => ComputeExpensesByCategoryAsync(y, m, ct),
            cancellationToken);
    }

    public Task<IReadOnlyList<MonthlyDynamicsPointDto>> GetMonthlyDynamicsAsync(
        int months, CancellationToken cancellationToken = default)
    {
        var userId = RequireUser();
        var count = Math.Clamp(months, 1, 24);
        var now = DateTimeOffset.UtcNow;

        return cache.GetOrSetAsync(
            $"dashboard:monthly-dynamics:{userId}:{count}:{now.Year}-{now.Month}",
            userId,
            ct => ComputeMonthlyDynamicsAsync(count, now, ct),
            cancellationToken);
    }

    public Task<IReadOnlyList<TopCategoryDto>> GetTopCategoriesAsync(
        int limit, int? year, int? month, CancellationToken cancellationToken = default)
    {
        var userId = RequireUser();
        var take = Math.Clamp(limit, 1, 20);
        var (y, m) = Normalize(year, month);

        return cache.GetOrSetAsync(
            $"dashboard:top-categories:{userId}:{take}:{y}-{m}",
            userId,
            async ct =>
            {
                var all = await ComputeExpensesByCategoryAsync(y, m, ct);
                return (IReadOnlyList<TopCategoryDto>)all
                    .Take(take)
                    .Select(e => new TopCategoryDto(e.CategoryId, e.CategoryName, e.Color, e.Icon, e.Amount))
                    .ToList();
            },
            cancellationToken);
    }

    // ── Computation ───────────────────────────────────────────────────────────

    private async Task<DashboardSummaryDto> ComputeSummaryAsync(int year, int month, CancellationToken ct)
    {
        var from = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1);

        // Each row is converted to the base currency via its captured rate
        // (Epic 8 — mirrors Accrual.AmountInBaseCurrency): a null rate is 1:1.
        var month_ = await db.Accruals
            .Where(a => a.IncludeInStats && a.Date >= from && a.Date < to)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Income = g.Sum(x => (x.Type == AccrualType.Income ? x.Amount
                    : x.Type == AccrualType.ReturnIncome ? -x.Amount : 0m) * (x.ExchangeRate ?? 1m)),
                Expense = g.Sum(x => (x.Type == AccrualType.Expense ? x.Amount
                    : x.Type == AccrualType.ReturnExpense ? -x.Amount : 0m) * (x.ExchangeRate ?? 1m)),
            })
            .FirstOrDefaultAsync(ct);

        var totals = await db.Accruals
            .Where(a => a.IncludeInStats)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Income = g.Sum(x => (x.Type == AccrualType.Income ? x.Amount
                    : x.Type == AccrualType.ReturnIncome ? -x.Amount : 0m) * (x.ExchangeRate ?? 1m)),
                Expense = g.Sum(x => (x.Type == AccrualType.Expense ? x.Amount
                    : x.Type == AccrualType.ReturnExpense ? -x.Amount : 0m) * (x.ExchangeRate ?? 1m)),
            })
            .FirstOrDefaultAsync(ct);

        var monthIncome = month_?.Income ?? 0m;
        var monthExpense = month_?.Expense ?? 0m;
        var totalBalance = (totals?.Income ?? 0m) - (totals?.Expense ?? 0m);

        return new DashboardSummaryDto(
            totalBalance, monthIncome, monthExpense, monthIncome - monthExpense, year, month);
    }

    private async Task<IReadOnlyList<ExpenseByCategoryDto>> ComputeExpensesByCategoryAsync(
        int year, int month, CancellationToken ct)
    {
        var from = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1);

        var raw = await db.Accruals
            .Where(a => a.IncludeInStats && a.Date >= from && a.Date < to
                && (a.Type == AccrualType.Expense || a.Type == AccrualType.ReturnExpense))
            .GroupBy(a => a.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                // Converted to base currency (Epic 8); null rate is 1:1.
                Amount = g.Sum(x => (x.Type == AccrualType.ReturnExpense ? -x.Amount : x.Amount) * (x.ExchangeRate ?? 1m)),
            })
            .ToListAsync(ct);

        var spent = raw.Where(r => r.Amount > 0m).ToList();
        if (spent.Count == 0)
            return [];

        var ids = spent.Where(r => r.CategoryId.HasValue).Select(r => r.CategoryId!.Value).ToList();
        var categories = await db.Categories
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.Color, c.Icon })
            .ToListAsync(ct);
        var byId = categories.ToDictionary(c => c.Id);

        var total = spent.Sum(r => r.Amount);

        return spent
            .OrderByDescending(r => r.Amount)
            .Select(r =>
            {
                var meta = r.CategoryId.HasValue && byId.TryGetValue(r.CategoryId.Value, out var c) ? c : null;
                return new ExpenseByCategoryDto(
                    r.CategoryId,
                    meta?.Name ?? UncategorizedName,
                    meta?.Color ?? UncategorizedColor,
                    meta?.Icon ?? UncategorizedIcon,
                    r.Amount,
                    total > 0m ? (double)Math.Round(r.Amount / total * 100m, 1) : 0d);
            })
            .ToList();
    }

    private async Task<IReadOnlyList<MonthlyDynamicsPointDto>> ComputeMonthlyDynamicsAsync(
        int months, DateTimeOffset now, CancellationToken ct)
    {
        var firstOfCurrent = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var from = firstOfCurrent.AddMonths(-(months - 1));
        var to = firstOfCurrent.AddMonths(1);

        var raw = await db.Accruals
            .Where(a => a.IncludeInStats && a.Date >= from && a.Date < to)
            .GroupBy(a => new { a.Date.Year, a.Date.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                // Converted to base currency (Epic 8); null rate is 1:1.
                Income = g.Sum(x => (x.Type == AccrualType.Income ? x.Amount
                    : x.Type == AccrualType.ReturnIncome ? -x.Amount : 0m) * (x.ExchangeRate ?? 1m)),
                Expense = g.Sum(x => (x.Type == AccrualType.Expense ? x.Amount
                    : x.Type == AccrualType.ReturnExpense ? -x.Amount : 0m) * (x.ExchangeRate ?? 1m)),
            })
            .ToListAsync(ct);

        var byMonth = raw.ToDictionary(r => (r.Year, r.Month));

        // Emit every month in the window, zero-filling gaps so the chart is dense.
        var points = new List<MonthlyDynamicsPointDto>(months);
        for (var i = 0; i < months; i++)
        {
            var d = from.AddMonths(i);
            points.Add(byMonth.TryGetValue((d.Year, d.Month), out var r)
                ? new MonthlyDynamicsPointDto(d.Year, d.Month, r.Income, r.Expense)
                : new MonthlyDynamicsPointDto(d.Year, d.Month, 0m, 0m));
        }

        return points;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private Guid RequireUser() =>
        currentUser.UserId ?? throw new ForbiddenAccessException("Authentication required.");

    private static (int Year, int Month) Normalize(int? year, int? month)
    {
        var now = DateTimeOffset.UtcNow;
        var y = year ?? now.Year;
        var m = month is >= 1 and <= 12 ? month.Value : now.Month;
        return (y, m);
    }
}
