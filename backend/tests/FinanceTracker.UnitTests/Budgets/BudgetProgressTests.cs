using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Budgets;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.UnitTests.Budgets;

/// <summary>
/// Verifies budget spend-progress calculation (T5.1.3 / ARCHITECTURE.md test
/// scenario "BudgetProgressCalculator — прогресс рассчитывается верно"):
/// only in-stats expenses in the budget's month count, net of returns, and the
/// percentage is spent/limit×100 (overrun allowed past 100).
/// </summary>
public class BudgetProgressTests
{
    private static readonly Guid User = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Groceries = Guid.Parse("a1c00000-0000-7000-8000-000000000001");
    private static readonly Guid Transport = Guid.Parse("a1c00000-0000-7000-8000-000000000003");

    private sealed record StubCurrentUser(Guid? UserId, bool IsAdmin) : ICurrentUserService;

    private sealed class NoopUnitOfWork(AppDbContext ctx) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            ctx.SaveChangesAsync(cancellationToken);
    }

    private static AppDbContext NewContext(ICurrentUserService user, string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options, user);
    }

    private static DateTimeOffset On(int year, int month, int day) =>
        new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    private static void Seed(string db)
    {
        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: true), db);

        ctx.Categories.Add(Category.CreateSystem(Groceries, "Продукты", "shopping-cart", "#22c55e"));
        ctx.Categories.Add(Category.CreateSystem(Transport, "Транспорт", "car", "#3b82f6"));

        // Budgets for June 2026.
        ctx.MonthlyBudgets.Add(new MonthlyBudget(User, Groceries, 2026, 6, 10_000m));
        ctx.MonthlyBudgets.Add(new MonthlyBudget(User, Transport, 2026, 6, 5_000m));

        // Groceries June: 6000 + 2000 expense, minus a 1000 return → net 7000 (70%).
        ctx.Accruals.Add(new Accrual(User, 6_000m, On(2026, 6, 3), AccrualType.Expense, categoryId: Groceries));
        ctx.Accruals.Add(new Accrual(User, 2_000m, On(2026, 6, 10), AccrualType.Expense, categoryId: Groceries));
        ctx.Accruals.Add(new Accrual(User, 1_000m, On(2026, 6, 12), AccrualType.ReturnExpense, categoryId: Groceries));

        // Excluded from stats — must NOT count.
        ctx.Accruals.Add(new Accrual(User, 9_999m, On(2026, 6, 15), AccrualType.Expense, categoryId: Groceries, includeInStats: false));

        // Wrong month — must NOT count.
        ctx.Accruals.Add(new Accrual(User, 9_999m, On(2026, 5, 20), AccrualType.Expense, categoryId: Groceries));

        // Transport June: 6000 expense over a 5000 limit → 120% overrun.
        ctx.Accruals.Add(new Accrual(User, 6_000m, On(2026, 6, 8), AccrualType.Expense, categoryId: Transport));

        ctx.SaveChanges();
    }

    private static BudgetService ServiceFor(AppDbContext ctx) =>
        new(ctx, new NoopUnitOfWork(ctx), ctx.CurrentUser);

    [Fact]
    public async Task Progress_CountsNetInStatsExpenses_InBudgetMonth()
    {
        const string db = nameof(Progress_CountsNetInStatsExpenses_InBudgetMonth);
        Seed(db);

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var progress = await ServiceFor(ctx).GetProgressAsync(2026, 6);

        var groceries = progress.Single(p => p.CategoryId == Groceries);
        Assert.Equal(7_000m, groceries.SpentAmount);
        Assert.Equal(10_000m, groceries.LimitAmount);
        Assert.Equal(3_000m, groceries.RemainingAmount);
        Assert.Equal(70d, groceries.Percentage);
    }

    [Fact]
    public async Task Progress_AllowsOverrunAboveHundredPercent()
    {
        const string db = nameof(Progress_AllowsOverrunAboveHundredPercent);
        Seed(db);

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var progress = await ServiceFor(ctx).GetProgressAsync(2026, 6);

        var transport = progress.Single(p => p.CategoryId == Transport);
        Assert.Equal(6_000m, transport.SpentAmount);
        Assert.Equal(-1_000m, transport.RemainingAmount);
        Assert.Equal(120d, transport.Percentage);
    }

    [Fact]
    public async Task Progress_ReturnsEmpty_WhenNoBudgetsForMonth()
    {
        const string db = nameof(Progress_ReturnsEmpty_WhenNoBudgetsForMonth);
        Seed(db);

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var progress = await ServiceFor(ctx).GetProgressAsync(2026, 7);

        Assert.Empty(progress);
    }

    [Fact]
    public async Task Progress_OrdersByPercentageDescending()
    {
        const string db = nameof(Progress_OrdersByPercentageDescending);
        Seed(db);

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var progress = await ServiceFor(ctx).GetProgressAsync(2026, 6);

        Assert.Equal(Transport, progress[0].CategoryId); // 120% before 70%
        Assert.Equal(Groceries, progress[1].CategoryId);
    }
}
