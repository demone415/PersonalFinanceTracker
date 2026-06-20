using FinanceTracker.Application.Common.Exceptions;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Budgets;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.UnitTests.Budgets;

/// <summary>
/// Covers the budget CRUD paths (T5.1.2): create persists and is readable, a
/// duplicate (same category + month) is rejected with 409, an unknown category
/// is rejected with 404, update mutates only the limit/currency, delete removes,
/// and another user cannot see or mutate the budget (data isolation).
/// </summary>
public class BudgetServiceTests
{
    private static readonly Guid User = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Other = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    private static readonly Guid Groceries = Guid.Parse("a1c00000-0000-7000-8000-000000000001");
    private static readonly Guid MissingCategory = Guid.Parse("a1c00000-0000-7000-8000-0000000000ff");

    private sealed record StubCurrentUser(Guid? UserId, bool IsAdmin) : ICurrentUserService;

    private sealed class NoopUnitOfWork(AppDbContext ctx) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            ctx.SaveChangesAsync(cancellationToken);

        public void DiscardChanges() => ctx.ChangeTracker.Clear();
    }

    private static AppDbContext NewContext(ICurrentUserService user, string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options, user);

    private static BudgetService ServiceFor(AppDbContext ctx) =>
        new(ctx, new NoopUnitOfWork(ctx), ctx.CurrentUser);

    private static void SeedCategory(string db)
    {
        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: true), db);
        ctx.Categories.Add(Category.CreateSystem(Groceries, "Продукты", "shopping-cart", "#22c55e"));
        ctx.SaveChanges();
    }

    private static CreateBudgetRequest NewRequest(decimal limit = 10_000m, string currency = "RUB") =>
        new(Groceries, 2026, 6, limit, currency);

    [Fact]
    public async Task Create_PersistsAndIsReadable()
    {
        const string db = nameof(Create_PersistsAndIsReadable);
        SeedCategory(db);

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var created = await ServiceFor(ctx).CreateAsync(NewRequest());

        Assert.Equal(Groceries, created.CategoryId);
        Assert.Equal("Продукты", created.CategoryName);
        Assert.Equal(10_000m, created.LimitAmount);

        var fetched = await ServiceFor(ctx).GetByIdAsync(created.Id);
        Assert.Equal(created.Id, fetched.Id);
    }

    [Fact]
    public async Task Create_DuplicateCategoryAndMonth_ThrowsConflict()
    {
        const string db = nameof(Create_DuplicateCategoryAndMonth_ThrowsConflict);
        SeedCategory(db);

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        await ServiceFor(ctx).CreateAsync(NewRequest());

        await Assert.ThrowsAsync<ConflictException>(() => ServiceFor(ctx).CreateAsync(NewRequest()));
    }

    [Fact]
    public async Task Create_UnknownCategory_ThrowsNotFound()
    {
        const string db = nameof(Create_UnknownCategory_ThrowsNotFound);
        SeedCategory(db);

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var request = new CreateBudgetRequest(MissingCategory, 2026, 6, 10_000m, "RUB");

        await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(ctx).CreateAsync(request));
    }

    [Fact]
    public async Task Update_ChangesLimitAndCurrency()
    {
        const string db = nameof(Update_ChangesLimitAndCurrency);
        SeedCategory(db);

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var created = await ServiceFor(ctx).CreateAsync(NewRequest());

        var updated = await ServiceFor(ctx).UpdateAsync(created.Id, new UpdateBudgetRequest(25_000m, "USD"));

        Assert.Equal(25_000m, updated.LimitAmount);
        Assert.Equal("USD", updated.Currency);
        Assert.Equal(Groceries, updated.CategoryId); // category is immutable
    }

    [Fact]
    public async Task Delete_RemovesBudget()
    {
        const string db = nameof(Delete_RemovesBudget);
        SeedCategory(db);

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var created = await ServiceFor(ctx).CreateAsync(NewRequest());

        await ServiceFor(ctx).DeleteAsync(created.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(ctx).GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task OtherUser_CannotSeeOrDeleteBudget()
    {
        const string db = nameof(OtherUser_CannotSeeOrDeleteBudget);
        SeedCategory(db);

        Guid budgetId;
        using (var owner = NewContext(new StubCurrentUser(User, IsAdmin: false), db))
            budgetId = (await ServiceFor(owner).CreateAsync(NewRequest())).Id;

        // The global query filter hides it from another user → not found, not 403.
        using var intruder = NewContext(new StubCurrentUser(Other, IsAdmin: false), db);
        await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(intruder).GetByIdAsync(budgetId));
        await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(intruder).DeleteAsync(budgetId));
    }
}
