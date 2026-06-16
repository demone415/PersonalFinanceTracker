using System.Reflection;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.ChangeLog;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.UnitTests.ChangeLog;

using ChangeLogEntity = FinanceTracker.Domain.Entities.ChangeLog;

/// <summary>
/// Covers the change-log read service (T7.1.1): a user sees only their own rows,
/// an admin sees everyone's (audit view), the entity-type and action filters narrow
/// the result, pagination honours page/pageSize and reports totals, and rows come
/// back newest-first.
/// </summary>
public class ChangeLogServiceTests
{
    private static readonly Guid User = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Other = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    private sealed record StubCurrentUser(Guid? UserId, bool IsAdmin) : ICurrentUserService;

    private static AppDbContext NewContext(ICurrentUserService user, string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options, user);

    /// <summary>Timestamp has a private setter; reflection lets tests pin it for ordering.</summary>
    private static ChangeLogEntity Log(
        Guid userId, string entityType, string action, DateTimeOffset timestamp)
    {
        var log = new ChangeLogEntity(userId, entityType, Guid.CreateVersion7(), action, null, "{}");
        typeof(ChangeLogEntity).GetProperty(nameof(ChangeLogEntity.Timestamp))!
            .SetValue(log, timestamp);
        return log;
    }

    private static void Seed(string db, params ChangeLogEntity[] logs)
    {
        // Admin context bypasses the isolation filter so cross-user rows persist.
        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: true), db);
        ctx.ChangeLogs.AddRange(logs);
        ctx.SaveChanges();
    }

    private static readonly DateTimeOffset T0 = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetPaged_RegularUser_SeesOnlyOwnRows()
    {
        const string db = nameof(GetPaged_RegularUser_SeesOnlyOwnRows);
        Seed(db,
            Log(User, "Accrual", "Create", T0),
            Log(User, "MonthlyBudget", "Update", T0.AddMinutes(1)),
            Log(Other, "Accrual", "Delete", T0.AddMinutes(2)));

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var result = await new ChangeLogService(ctx).GetPagedAsync(new ChangeLogFilterRequest());

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, e => Assert.Equal(User, e.UserId));
    }

    [Fact]
    public async Task GetPaged_Admin_SeesAllRows()
    {
        const string db = nameof(GetPaged_Admin_SeesAllRows);
        Seed(db,
            Log(User, "Accrual", "Create", T0),
            Log(Other, "Accrual", "Create", T0.AddMinutes(1)));

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: true), db);
        var result = await new ChangeLogService(ctx).GetPagedAsync(new ChangeLogFilterRequest());

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetPaged_FilterByEntityType_ReturnsMatchingOnly()
    {
        const string db = nameof(GetPaged_FilterByEntityType_ReturnsMatchingOnly);
        Seed(db,
            Log(User, "Accrual", "Create", T0),
            Log(User, "MonthlyBudget", "Create", T0.AddMinutes(1)),
            Log(User, "Accrual", "Update", T0.AddMinutes(2)));

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var result = await new ChangeLogService(ctx)
            .GetPagedAsync(new ChangeLogFilterRequest(EntityType: "Accrual"));

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, e => Assert.Equal("Accrual", e.EntityType));
    }

    [Fact]
    public async Task GetPaged_FilterByAction_ReturnsMatchingOnly()
    {
        const string db = nameof(GetPaged_FilterByAction_ReturnsMatchingOnly);
        Seed(db,
            Log(User, "Accrual", "Create", T0),
            Log(User, "Accrual", "Delete", T0.AddMinutes(1)));

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var result = await new ChangeLogService(ctx)
            .GetPagedAsync(new ChangeLogFilterRequest(Action: "Delete"));

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Delete", Assert.Single(result.Items).Action);
    }

    [Fact]
    public async Task GetPaged_OrdersNewestFirstAndPaginates()
    {
        const string db = nameof(GetPaged_OrdersNewestFirstAndPaginates);
        Seed(db,
            Log(User, "Accrual", "Create", T0),
            Log(User, "Accrual", "Update", T0.AddMinutes(1)),
            Log(User, "Accrual", "Delete", T0.AddMinutes(2)));

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var page1 = await new ChangeLogService(ctx)
            .GetPagedAsync(new ChangeLogFilterRequest(Page: 1, PageSize: 2));

        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(2, page1.Items.Count);
        // Newest first: Delete (T0+2) then Update (T0+1).
        Assert.Equal("Delete", page1.Items[0].Action);
        Assert.Equal("Update", page1.Items[1].Action);

        var page2 = await new ChangeLogService(ctx)
            .GetPagedAsync(new ChangeLogFilterRequest(Page: 2, PageSize: 2));
        Assert.Equal("Create", Assert.Single(page2.Items).Action);
    }
}
