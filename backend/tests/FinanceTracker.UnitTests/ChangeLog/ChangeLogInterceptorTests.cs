using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.UnitTests.ChangeLog;

/// <summary>
/// The <see cref="ChangeLogInterceptor"/> attribution rule: an HTTP request logs
/// under the JWT caller, but a background mutation (no HTTP user — e.g. FNS import)
/// falls back to the owned entity's own UserId so its changes still reach the
/// per-user Журнал instead of being silently dropped.
/// </summary>
public class ChangeLogInterceptorTests
{
    private static readonly Guid Owner = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private sealed record StubCurrentUser(Guid? UserId, bool IsAdmin) : ICurrentUserService;

    private static AppDbContext NewContext(string name, ICurrentUserService caller)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"changelog-{name}")
            .AddInterceptors(new ChangeLogInterceptor(caller))
            .Options;
        // The query filter resolves the caller from the context; admin bypass keeps
        // the post-save read visible regardless of who the interceptor attributed to.
        return new AppDbContext(options, new StubCurrentUser(null, IsAdmin: true));
    }

    [Fact]
    public async Task BackgroundCreate_WithoutHttpUser_LogsUnderEntityOwner()
    {
        await using var db = NewContext(nameof(BackgroundCreate_WithoutHttpUser_LogsUnderEntityOwner),
            new StubCurrentUser(UserId: null, IsAdmin: false));

        db.Accruals.Add(new Accrual(
            Owner, 100m, DateTimeOffset.UtcNow, AccrualType.Expense,
            currency: "RUB", description: "Импорт ФНС", includeInStats: true));
        await db.SaveChangesAsync();

        var log = await db.ChangeLogs.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(Owner, log.UserId);
        Assert.Equal("Accrual", log.EntityType);
        Assert.Equal("Create", log.Action);
    }

    [Fact]
    public async Task HttpCreate_LogsUnderJwtCaller()
    {
        var caller = Guid.Parse("00000000-0000-0000-0000-0000000000c3");
        await using var db = NewContext(nameof(HttpCreate_LogsUnderJwtCaller),
            new StubCurrentUser(UserId: caller, IsAdmin: false));

        db.Accruals.Add(new Accrual(
            Owner, 100m, DateTimeOffset.UtcNow, AccrualType.Expense,
            currency: "RUB", description: "x", includeInStats: true));
        await db.SaveChangesAsync();

        var log = await db.ChangeLogs.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(caller, log.UserId);
    }

    [Fact]
    public async Task ReceiptFetch_FoldsAddedItems_IntoLinkedAccrualEntry()
    {
        await using var db = NewContext(nameof(ReceiptFetch_FoldsAddedItems_IntoLinkedAccrualEntry),
            new StubCurrentUser(UserId: null, IsAdmin: false));

        // A QR-scanned accrual already exists, linked to a pending (item-less) receipt.
        var receipt = Receipt.CreateForQrScan(Owner, 0, DateTimeOffset.UtcNow, qrRaw: "t=...");
        var accrual = new Accrual(
            Owner, 0m, DateTimeOffset.UtcNow, AccrualType.Expense,
            currency: "RUB", description: Accrual.PendingReceiptDescription, includeInStats: true);
        accrual.SetReceipt(receipt.Id);
        db.Receipts.Add(receipt);
        db.Accruals.Add(accrual);
        await db.SaveChangesAsync();

        // The fetch arrives: items appear on the receipt and the accrual is refreshed.
        receipt.AddItem(new ReceiptItem(receipt.Id, "Хлеб", 50m, 1m, 50m));
        receipt.AddItem(new ReceiptItem(receipt.Id, "Молоко", 80m, 1m, 80m));
        accrual.ApplyFetchedReceipt("Пятёрочка", 130m);
        await db.SaveChangesAsync();

        var update = await db.ChangeLogs.IgnoreQueryFilters()
            .Where(l => l.Action == "Update")
            .SingleAsync();
        var after = System.Text.Json.Nodes.JsonNode.Parse(update.ValuesAfter!)!.AsObject();
        var items = after["Items"]!.AsArray();
        Assert.Equal(2, items.Count);
        Assert.Contains(items, n => (string?)n!["Name"] == "Хлеб");
        Assert.Contains(items, n => (string?)n!["Name"] == "Молоко");
    }
}
