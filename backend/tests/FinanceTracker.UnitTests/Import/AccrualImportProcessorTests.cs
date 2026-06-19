using System.Text;
using System.Text.Json;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Import;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.UnitTests.Import;

/// <summary>
/// The off-request FNS import decision tree (Story 6.1): owner-scoped dedup by
/// (number, INN, date), accrual + linked receipt + item creation, the JSON summary,
/// idempotency, the terminal bad-file path and the bounded transient-retry path —
/// driven through the real <see cref="AccrualImportProcessor"/> against an in-memory
/// database with a stub parser and a fake object store.
/// </summary>
public class AccrualImportProcessorTests
{
    private static readonly Guid Owner = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Other = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    private static readonly DateTimeOffset Now = new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);
    private const string SourceKey = "imports/src/test.xlsx";

    private sealed record StubCurrentUser(Guid? UserId, bool IsAdmin) : ICurrentUserService;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubParser(Func<IReadOnlyList<ParsedReceipt>> factory) : IFnsReceiptParser
    {
        public IReadOnlyList<ParsedReceipt> Parse(Stream excel) => factory();
    }

    private sealed class FakeFileStorage : IFileStorage
    {
        public readonly Dictionary<string, byte[]> Objects = new();
        public int UploadCount { get; private set; }
        public Action? OnUpload { get; set; }

        public async Task UploadAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            OnUpload?.Invoke();
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, cancellationToken);
            Objects[objectKey] = ms.ToArray();
            UploadCount++;
        }

        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(Objects[objectKey]));

        public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(Objects.ContainsKey(objectKey));

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            Objects.Remove(objectKey);
            return Task.CompletedTask;
        }
    }

    private sealed class Harness
    {
        public AppDbContext Db { get; }
        public FakeFileStorage Storage { get; } = new();
        public Func<IReadOnlyList<ParsedReceipt>> ParseResult { get; set; } = () => [];

        public Harness(string dbName)
        {
            // Namespace the store name: EF InMemory keys by name on a process-wide
            // static root, so an unprefixed nameof() would collide with same-named
            // tests in other classes (e.g. the export suite) running in parallel.
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"import-{dbName}")
                .Options;
            Db = new AppDbContext(options, new StubCurrentUser(null, IsAdmin: true));
            // The processor opens the stored source before parsing; the stub parser
            // ignores its content, so any bytes under the key will do.
            Storage.Objects[SourceKey] = "xlsx"u8.ToArray();
        }

        public AccrualImportProcessor Processor() => new(
            Db, new UnitOfWork(Db), new StubParser(ParseResult), Storage,
            new FixedTimeProvider(Now), NullLogger<AccrualImportProcessor>.Instance);

        public BackgroundTask SeedTask(Guid userId)
        {
            var task = new BackgroundTask(userId, BackgroundTaskType.ImportFns);
            Db.BackgroundTasks.Add(task);
            Db.SaveChanges();
            return task;
        }

        public void SeedExistingReceipt(Guid userId, string number, string inn, DateTimeOffset date)
        {
            Db.Receipts.Add(Receipt.CreateImported(userId, number, 100, date, organization: "x", inn: inn));
            Db.SaveChanges();
        }

        public BackgroundTask Reload(Guid id) =>
            Db.BackgroundTasks.IgnoreQueryFilters().AsNoTracking().Single(t => t.Id == id);

        public ImportSummary ReadSummary(string key) =>
            JsonSerializer.Deserialize<ImportSummary>(Encoding.UTF8.GetString(Storage.Objects[key]))!;
    }

    private static ParsedReceipt MakeReceipt(
        string number, string inn, DateTimeOffset date, decimal total, params ParsedReceiptItem[] items) =>
        new(number, inn, "Магазин", "Адрес", date, total, Category: null, Description: null,
            Items: items.Length == 0 ? [new ParsedReceiptItem("Товар", total, 1, total)] : items);

    [Fact]
    public async Task Import_CreatesAccrualReceiptItems_AndMarksDone()
    {
        var h = new Harness(nameof(Import_CreatesAccrualReceiptItems_AndMarksDone));
        h.ParseResult = () =>
        [
            MakeReceipt("39", "2540167061", Now.AddDays(-1), 369.98m,
                new ParsedReceiptItem("Креветки", 184.99m, 2, 369.98m),
                new ParsedReceiptItem("Сыр", 169.99m, 1, 169.99m)),
            MakeReceipt("40", "2540167061", Now.AddDays(-2), 1000m),
        ];
        var task = h.SeedTask(Owner);

        var result = await h.Processor().ProcessAsync(task.Id, SourceKey);

        Assert.Equal(AccrualImportOutcome.Completed, result.Status);
        var saved = h.Reload(task.Id);
        Assert.Equal(BackgroundTaskStatus.Done, saved.Status);

        var accruals = h.Db.Accruals.IgnoreQueryFilters().Where(a => a.UserId == Owner).ToList();
        Assert.Equal(2, accruals.Count);
        Assert.All(accruals, a => Assert.Equal(AccrualType.Expense, a.Type));
        Assert.All(accruals, a => Assert.NotNull(a.ReceiptId));
        Assert.Equal(3, h.Db.ReceiptItems.IgnoreQueryFilters().Count()); // 2 + 1

        var summary = h.ReadSummary(saved.ResultObjectKey);
        Assert.Equal(2, summary.ReceiptsImported);
        Assert.Equal(0, summary.ReceiptsSkippedDuplicate);
        Assert.Equal(2, summary.ReceiptsTotal);
    }

    [Fact]
    public async Task Duplicate_ByNumberInnDate_IsSkipped()
    {
        var h = new Harness(nameof(Duplicate_ByNumberInnDate_IsSkipped));
        var dupDate = Now.AddDays(-1);
        h.SeedExistingReceipt(Owner, "39", "2540167061", dupDate);
        h.ParseResult = () =>
        [
            MakeReceipt("39", "2540167061", dupDate, 369.98m),   // already exists → skip
            MakeReceipt("41", "2540167061", Now.AddDays(-3), 500m), // new → import
        ];
        var task = h.SeedTask(Owner);

        await h.Processor().ProcessAsync(task.Id, SourceKey);

        var summary = h.ReadSummary(h.Reload(task.Id).ResultObjectKey);
        Assert.Equal(1, summary.ReceiptsImported);
        Assert.Equal(1, summary.ReceiptsSkippedDuplicate);
        // One pre-existing + one imported = 2 receipts; only one new accrual.
        Assert.Equal(2, h.Db.Receipts.IgnoreQueryFilters().Count(r => r.UserId == Owner));
        Assert.Single(h.Db.Accruals.IgnoreQueryFilters().Where(a => a.UserId == Owner));
    }

    [Fact]
    public async Task Dedup_IsScopedToOwner_NotOtherUsers()
    {
        var h = new Harness(nameof(Dedup_IsScopedToOwner_NotOtherUsers));
        var date = Now.AddDays(-1);
        h.SeedExistingReceipt(Other, "39", "2540167061", date); // same key, different owner
        h.ParseResult = () => [MakeReceipt("39", "2540167061", date, 369.98m)];
        var task = h.SeedTask(Owner);

        await h.Processor().ProcessAsync(task.Id, SourceKey);

        var summary = h.ReadSummary(h.Reload(task.Id).ResultObjectKey);
        Assert.Equal(1, summary.ReceiptsImported);
        Assert.Equal(0, summary.ReceiptsSkippedDuplicate);
    }

    [Fact]
    public async Task BadFile_FailsTerminally_WithoutRetry()
    {
        var h = new Harness(nameof(BadFile_FailsTerminally_WithoutRetry));
        h.ParseResult = () => throw new FnsImportFormatException("bad");
        var task = h.SeedTask(Owner);

        var result = await h.Processor().ProcessAsync(task.Id, SourceKey);

        Assert.Equal(AccrualImportOutcome.Failed, result.Status);
        var saved = h.Reload(task.Id);
        Assert.Equal(BackgroundTaskStatus.Failed, saved.Status);
        Assert.Equal(AccrualImportProcessor.BadFormatMessage, saved.Error);
        Assert.Equal(0, saved.Attempts); // not counted against the transient-retry budget
    }

    [Fact]
    public async Task AlreadyDone_IsIdempotentNoOp()
    {
        var h = new Harness(nameof(AlreadyDone_IsIdempotentNoOp));
        h.ParseResult = () => [MakeReceipt("39", "2540167061", Now, 100m)];
        var task = h.SeedTask(Owner);

        await h.Processor().ProcessAsync(task.Id, SourceKey);
        var second = await h.Processor().ProcessAsync(task.Id, SourceKey);

        Assert.Equal(AccrualImportOutcome.Skipped, second.Status);
        Assert.Single(h.Db.Accruals.IgnoreQueryFilters().Where(a => a.UserId == Owner));
    }

    [Fact]
    public async Task TransientFailure_IsRescheduled_AndKeepsTaskRunning()
    {
        var h = new Harness(nameof(TransientFailure_IsRescheduled_AndKeepsTaskRunning));
        h.ParseResult = () => [MakeReceipt("39", "2540167061", Now, 100m)];
        var task = h.SeedTask(Owner);
        h.Storage.OnUpload = () => throw new InvalidOperationException("storage exploded");

        var result = await h.Processor().ProcessAsync(task.Id, SourceKey);

        Assert.Equal(AccrualImportOutcome.Rescheduled, result.Status);
        Assert.NotNull(result.RetryDelay);
        var saved = h.Reload(task.Id);
        Assert.Equal(BackgroundTaskStatus.Running, saved.Status);
        Assert.Equal(1, saved.Attempts);
    }
}
