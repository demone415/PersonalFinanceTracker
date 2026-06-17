using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Budgets;
using FinanceTracker.Application.Features.Dashboard;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FinanceTracker.IntegrationTests;

/// <summary>
/// Currency-aggregation contract (Epic 8, T8.1.4) against real PostgreSQL
/// (Testcontainers), as required by CLAUDE.md. The unit tests cover the same maths
/// on the EF in-memory provider, which evaluates LINQ in C# and therefore cannot
/// prove the conversion expression — <c>(CASE … ) * COALESCE("ExchangeRate", 1)</c>
/// inside <c>SUM</c> with a <c>GROUP BY</c> — actually translates to and runs as
/// SQL. This exercises all three aggregation sites end to end:
/// <see cref="DashboardService"/> summary + expenses-by-category and
/// <see cref="BudgetService.GetProgressAsync"/>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CurrencyAggregationPostgresTests : IAsyncLifetime
{
    private static readonly Guid User = Guid.Parse("00000000-0000-0000-0000-0000000000e1");
    private static readonly Guid Groceries = Guid.Parse("a1c00000-0000-7000-8000-0000000000e2");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private string? _startupError;

    public async Task InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();

            // Our migrations add raw-SQL FKs to the GoTrue-owned auth.users table,
            // which a bare Postgres image lacks. Create a minimal stub and a row for
            // the test user so the categories/monthly_budgets FKs are satisfiable.
            await using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false));
            await ctx.Database.ExecuteSqlRawAsync(
                "CREATE SCHEMA IF NOT EXISTS auth; " +
                "CREATE TABLE IF NOT EXISTS auth.users (id uuid PRIMARY KEY);");
            await ctx.Database.MigrateAsync();
            await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO auth.users (id) VALUES ({User}) ON CONFLICT DO NOTHING;");
        }
        catch (Exception ex) when (ex is not Xunit.SkipException)
        {
            // No Docker / image pull failure → skip rather than fail the suite.
            _startupError = ex.Message;
        }
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private sealed record StubCurrentUser(Guid? UserId, bool IsAdmin) : ICurrentUserService;

    // Pass-through cache: always computes, never stores — isolates the aggregation.
    private sealed class PassThroughCache : IDashboardCache
    {
        public Task<T> GetOrSetAsync<T>(
            string key, Guid userId, Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken = default) => factory(cancellationToken);

        public Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private AppDbContext NewContext(ICurrentUserService user)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new AppDbContext(options, user);
    }

    private static DateTimeOffset On(int year, int month, int day) =>
        new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    [SkippableFact]
    public async Task Aggregates_ConvertEachAccrualToBaseCurrency_AgainstRealPostgres()
    {
        Skip.If(_startupError is not null, $"Postgres container unavailable: {_startupError}");

        await using (var seed = NewContext(new StubCurrentUser(User, IsAdmin: false)))
        {
            seed.Categories.Add(Category.CreateSystem(Groceries, "Продукты", "shopping-cart", "#22c55e"));
            seed.MonthlyBudgets.Add(new MonthlyBudget(User, Groceries, 2026, 6, 10_000m));

            // Income: 1000 RUB (1:1) + 100 USD @ 90 = 9000 → 10 000.
            seed.Accruals.Add(new Accrual(User, 1_000m, On(2026, 6, 4), AccrualType.Income));
            seed.Accruals.Add(new Accrual(User, 100m, On(2026, 6, 5), AccrualType.Income,
                currency: "USD", exchangeRate: 90m));

            // Groceries spend (all converted): 2000 RUB (1:1) + 100 USD @ 80 = 8000,
            // minus a 25 USD @ 80 = 2000 return → 8000 net.
            seed.Accruals.Add(new Accrual(User, 2_000m, On(2026, 6, 6), AccrualType.Expense,
                categoryId: Groceries));
            seed.Accruals.Add(new Accrual(User, 100m, On(2026, 6, 7), AccrualType.Expense,
                currency: "USD", categoryId: Groceries, exchangeRate: 80m));
            seed.Accruals.Add(new Accrual(User, 25m, On(2026, 6, 8), AccrualType.ReturnExpense,
                currency: "USD", categoryId: Groceries, exchangeRate: 80m));

            await seed.SaveChangesAsync();
        }

        await using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false));
        var dashboard = new DashboardService(ctx, new PassThroughCache(), ctx.CurrentUser);
        var budgets = new BudgetService(ctx, new UnitOfWork(ctx), ctx.CurrentUser);

        var summary = await dashboard.GetSummaryAsync(2026, 6);
        Assert.Equal(10_000m, summary.MonthIncome);
        Assert.Equal(8_000m, summary.MonthExpense);
        Assert.Equal(2_000m, summary.MonthBalance);
        Assert.Equal(2_000m, summary.TotalBalance);

        var byCategory = await dashboard.GetExpensesByCategoryAsync(2026, 6);
        var groceriesExpense = byCategory.Single(c => c.CategoryId == Groceries);
        Assert.Equal(8_000m, groceriesExpense.Amount);

        var progress = await budgets.GetProgressAsync(2026, 6);
        var groceriesBudget = progress.Single(p => p.CategoryId == Groceries);
        Assert.Equal(8_000m, groceriesBudget.SpentAmount);
        Assert.Equal(2_000m, groceriesBudget.RemainingAmount);
        Assert.Equal(80d, groceriesBudget.Percentage);
    }
}
