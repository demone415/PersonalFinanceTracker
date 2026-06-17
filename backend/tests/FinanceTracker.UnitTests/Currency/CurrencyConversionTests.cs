using FinanceTracker.Application.Common.Exceptions;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Accruals;
using FinanceTracker.Application.Features.Budgets;
using FinanceTracker.Application.Features.Dashboard;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.UnitTests.Currency;

/// <summary>
/// Verifies multi-currency aggregation (Epic 8, T8.1.4): every aggregate converts
/// each accrual to the user's base currency via the rate captured at transaction
/// time (<see cref="Accrual.AmountInBaseCurrency"/> — a null rate is 1:1), and the
/// rule is applied consistently in <see cref="DashboardService"/> and
/// <see cref="BudgetService"/> so figures stay aligned.
/// </summary>
public class CurrencyConversionTests
{
    private static readonly Guid User = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid Groceries = Guid.Parse("a1c00000-0000-7000-8000-000000000001");

    private sealed record StubCurrentUser(Guid? UserId, bool IsAdmin) : ICurrentUserService;

    private sealed class NoopUnitOfWork(AppDbContext ctx) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            ctx.SaveChangesAsync(cancellationToken);
    }

    // Pass-through cache: always computes, never stores — isolates the aggregation.
    private sealed class PassThroughCache : IDashboardCache
    {
        public Task<T> GetOrSetAsync<T>(
            string key, Guid userId, Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken = default) => factory(cancellationToken);

        public Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static AppDbContext NewContext(ICurrentUserService user, string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options, user);

    private static DateTimeOffset On(int year, int month, int day) =>
        new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AmountInBaseCurrency_NullRateIsOneToOne_OtherwiseMultiplies()
    {
        var rub = new Accrual(User, 1_000m, On(2026, 6, 1), AccrualType.Expense);
        var usd = new Accrual(User, 100m, On(2026, 6, 1), AccrualType.Expense,
            currency: "USD", exchangeRate: 90m);

        Assert.Equal(1_000m, rub.AmountInBaseCurrency);
        Assert.Equal(9_000m, usd.AmountInBaseCurrency);
    }

    [Fact]
    public async Task DashboardSummary_ConvertsForeignAccrualsToBaseCurrency()
    {
        const string db = nameof(DashboardSummary_ConvertsForeignAccrualsToBaseCurrency);
        using (var seed = NewContext(new StubCurrentUser(User, IsAdmin: true), db))
        {
            // Income: 1000 RUB (1:1) + 100 USD @ 90 = 9000 → 10 000.
            seed.Accruals.Add(new Accrual(User, 1_000m, On(2026, 6, 4), AccrualType.Income));
            seed.Accruals.Add(new Accrual(User, 100m, On(2026, 6, 5), AccrualType.Income,
                currency: "USD", exchangeRate: 90m));
            // Expense: 500 RUB (1:1) + 10 EUR @ 100 = 1000 → 1500.
            seed.Accruals.Add(new Accrual(User, 500m, On(2026, 6, 6), AccrualType.Expense));
            seed.Accruals.Add(new Accrual(User, 10m, On(2026, 6, 7), AccrualType.Expense,
                currency: "EUR", exchangeRate: 100m));
            await seed.SaveChangesAsync();
        }

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var dashboard = new DashboardService(ctx, new PassThroughCache(), ctx.CurrentUser);

        var summary = await dashboard.GetSummaryAsync(2026, 6);

        Assert.Equal(10_000m, summary.MonthIncome);
        Assert.Equal(1_500m, summary.MonthExpense);
        Assert.Equal(8_500m, summary.MonthBalance);
        Assert.Equal(8_500m, summary.TotalBalance);
    }

    [Fact]
    public async Task ExpensesByCategory_ConvertsForeignAccrualsToBaseCurrency()
    {
        const string db = nameof(ExpensesByCategory_ConvertsForeignAccrualsToBaseCurrency);
        using (var seed = NewContext(new StubCurrentUser(User, IsAdmin: true), db))
        {
            seed.Categories.Add(Category.CreateSystem(Groceries, "Продукты", "shopping-cart", "#22c55e"));
            // 2000 RUB (1:1) + 50 USD @ 80 = 4000 → 6000 in Groceries.
            seed.Accruals.Add(new Accrual(User, 2_000m, On(2026, 6, 3), AccrualType.Expense, categoryId: Groceries));
            seed.Accruals.Add(new Accrual(User, 50m, On(2026, 6, 9), AccrualType.Expense,
                currency: "USD", categoryId: Groceries, exchangeRate: 80m));
            await seed.SaveChangesAsync();
        }

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var dashboard = new DashboardService(ctx, new PassThroughCache(), ctx.CurrentUser);

        var byCategory = await dashboard.GetExpensesByCategoryAsync(2026, 6);

        var groceries = byCategory.Single(c => c.CategoryId == Groceries);
        Assert.Equal(6_000m, groceries.Amount);
    }

    [Fact]
    public async Task BudgetProgress_ConvertsForeignSpendToBaseCurrency()
    {
        const string db = nameof(BudgetProgress_ConvertsForeignSpendToBaseCurrency);
        using (var seed = NewContext(new StubCurrentUser(User, IsAdmin: true), db))
        {
            seed.Categories.Add(Category.CreateSystem(Groceries, "Продукты", "shopping-cart", "#22c55e"));
            seed.MonthlyBudgets.Add(new MonthlyBudget(User, Groceries, 2026, 6, 10_000m));
            // Spend: 3000 RUB (1:1) + 20 USD @ 100 = 2000 → 5000 of a 10 000 limit = 50%.
            seed.Accruals.Add(new Accrual(User, 3_000m, On(2026, 6, 3), AccrualType.Expense, categoryId: Groceries));
            seed.Accruals.Add(new Accrual(User, 20m, On(2026, 6, 8), AccrualType.Expense,
                currency: "USD", categoryId: Groceries, exchangeRate: 100m));
            await seed.SaveChangesAsync();
        }

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var budgets = new BudgetService(ctx, new NoopUnitOfWork(ctx), ctx.CurrentUser);

        var progress = await budgets.GetProgressAsync(2026, 6);

        var groceries = progress.Single(p => p.CategoryId == Groceries);
        Assert.Equal(5_000m, groceries.SpentAmount);
        Assert.Equal(5_000m, groceries.RemainingAmount);
        Assert.Equal(50d, groceries.Percentage);
    }

    // ── Foreign-currency rate enforcement (the API is the source of truth) ───────

    private static AccrualService NewAccrualService(AppDbContext ctx) =>
        new(ctx, new NoopUnitOfWork(ctx), ctx.CurrentUser, new PassThroughCache(),
            NullLogger<AccrualService>.Instance);

    private static CreateAccrualRequest CreateRequest(string currency, decimal? exchangeRate) =>
        new(100m, On(2026, 6, 1), AccrualType.Expense, currency,
            CategoryId: null, Description: null, IncludeInStats: true, GroupId: null,
            ExchangeRate: exchangeRate, Tags: []);

    [Fact]
    public async Task Create_ForeignCurrencyWithoutRate_IsRejected()
    {
        const string db = nameof(Create_ForeignCurrencyWithoutRate_IsRejected);
        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var service = NewAccrualService(ctx);

        // No profile → base currency defaults to RUB, so USD without a rate is foreign.
        await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateAsync(CreateRequest("USD", exchangeRate: null)));
        Assert.False(await ctx.Accruals.AnyAsync());
    }

    [Fact]
    public async Task Create_ForeignCurrencyWithRate_IsAccepted()
    {
        const string db = nameof(Create_ForeignCurrencyWithRate_IsAccepted);
        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var service = NewAccrualService(ctx);

        var created = await service.CreateAsync(CreateRequest("USD", exchangeRate: 90m));

        Assert.Equal("USD", created.Currency);
        Assert.Equal(90m, created.ExchangeRate);
    }

    [Fact]
    public async Task Create_BaseCurrencyWithoutRate_IsAccepted()
    {
        const string db = nameof(Create_BaseCurrencyWithoutRate_IsAccepted);
        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var service = NewAccrualService(ctx);

        var created = await service.CreateAsync(CreateRequest("RUB", exchangeRate: null));

        Assert.Equal("RUB", created.Currency);
        Assert.Null(created.ExchangeRate);
    }

    [Fact]
    public async Task Create_HonoursProfileBaseCurrency_WhenNotRub()
    {
        const string db = nameof(Create_HonoursProfileBaseCurrency_WhenNotRub);
        using (var seed = NewContext(new StubCurrentUser(User, IsAdmin: false), db))
        {
            seed.UserProfiles.Add(new UserProfile(User, displayName: null, currency: "USD"));
            await seed.SaveChangesAsync();
        }

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var service = NewAccrualService(ctx);

        // USD is now the base — no rate needed; RUB has become the foreign currency.
        var usd = await service.CreateAsync(CreateRequest("USD", exchangeRate: null));
        Assert.Equal("USD", usd.Currency);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateAsync(CreateRequest("RUB", exchangeRate: null)));
    }
}
