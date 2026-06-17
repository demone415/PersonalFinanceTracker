using System.Text;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Export;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.UnitTests.Export;

/// <summary>
/// The off-request CSV export decision tree (T6.2.1): owner-scoped querying
/// (bypassing the data-isolation filter under a background no-user context),
/// filter application, terminal state transitions, idempotency, the bounded
/// retry path and the failure path — driven through the real
/// <see cref="AccrualExportProcessor"/> against an in-memory database with the
/// real CSV writer and a fake object store.
/// </summary>
public class AccrualExportProcessorTests
{
    private static readonly Guid Owner = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Other = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    private sealed record StubCurrentUser(Guid? UserId, bool IsAdmin) : ICurrentUserService;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>In-memory <see cref="IFileStorage"/> that records uploads and can be made to fail.</summary>
    private sealed class FakeFileStorage : IFileStorage
    {
        public readonly Dictionary<string, byte[]> Objects = new();
        public int UploadCount { get; private set; }

        /// <summary>Invoked at the start of each upload; throw from it to simulate a failure.</summary>
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
        public AccrualExportProcessor Processor { get; }

        public Harness(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            Db = new AppDbContext(options, new StubCurrentUser(null, IsAdmin: true));

            Processor = new AccrualExportProcessor(
                Db,
                new UnitOfWork(Db),
                new CsvAccrualExporter(),
                Storage,
                new FixedTimeProvider(Now),
                NullLogger<AccrualExportProcessor>.Instance);
        }

        public void SeedAccrual(
            Guid userId, decimal amount, AccrualType type = AccrualType.Expense,
            DateTimeOffset? date = null, string? description = null)
        {
            Db.Accruals.Add(new Accrual(userId, amount, date ?? Now, type, "RUB",
                categoryId: null, description: description, includeInStats: true));
            Db.SaveChanges();
        }

        public BackgroundTask SeedTask(Guid userId)
        {
            var task = new BackgroundTask(userId, BackgroundTaskType.ExportCsv);
            Db.BackgroundTasks.Add(task);
            Db.SaveChanges();
            return task;
        }

        public BackgroundTask Reload(Guid id) =>
            Db.BackgroundTasks.IgnoreQueryFilters().AsNoTracking().Single(t => t.Id == id);

        /// <summary>Number of data records (excluding the header) in the uploaded CSV.</summary>
        public string[] DataLines(string objectKey)
        {
            var text = StripBom(Storage.Objects[objectKey]);
            var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            return lines.Skip(1).ToArray();
        }

        private static string StripBom(byte[] bytes)
        {
            var bom = Encoding.UTF8.GetPreamble();
            var has = bytes.Length >= bom.Length && bytes.Take(bom.Length).SequenceEqual(bom);
            return Encoding.UTF8.GetString(has ? bytes[bom.Length..] : bytes);
        }
    }

    [Fact]
    public async Task Success_UploadsCsv_AndMarksTaskDone()
    {
        var h = new Harness(nameof(Success_UploadsCsv_AndMarksTaskDone));
        h.SeedAccrual(Owner, 100m, description: "groceries");
        h.SeedAccrual(Owner, 200m, description: "fuel");
        var task = h.SeedTask(Owner);

        var result = await h.Processor.ProcessAsync(task.Id, new AccrualExportFilter());

        Assert.Equal(AccrualExportOutcome.Completed, result.Status);
        var saved = h.Reload(task.Id);
        Assert.Equal(BackgroundTaskStatus.Done, saved.Status);
        Assert.Equal(100, saved.Progress);
        Assert.Equal(0, saved.Attempts);
        Assert.Equal(Now, saved.CompletedAt);
        Assert.Equal(1, h.Storage.UploadCount);
        // The upload targets the task's stable, pre-assigned object key.
        Assert.Equal(task.ResultObjectKey, saved.ResultObjectKey);
        Assert.True(h.Storage.Objects.ContainsKey(saved.ResultObjectKey));
        Assert.Equal(2, h.DataLines(saved.ResultObjectKey).Length);
    }

    [Fact]
    public async Task OnlyTheOwnersAccruals_AreExported()
    {
        var h = new Harness(nameof(OnlyTheOwnersAccruals_AreExported));
        h.SeedAccrual(Owner, 100m, description: "mine-a");
        h.SeedAccrual(Owner, 200m, description: "mine-b");
        h.SeedAccrual(Other, 999m, description: "not-mine");
        var task = h.SeedTask(Owner);

        await h.Processor.ProcessAsync(task.Id, new AccrualExportFilter());

        var key = h.Reload(task.Id).ResultObjectKey;
        var lines = h.DataLines(key);
        Assert.Equal(2, lines.Length);
        Assert.DoesNotContain(lines, l => l.Contains("not-mine"));
        Assert.DoesNotContain(lines, l => l.Contains("999"));
    }

    [Fact]
    public async Task Filters_AreApplied_ToTheExportedRows()
    {
        var h = new Harness(nameof(Filters_AreApplied_ToTheExportedRows));
        h.SeedAccrual(Owner, 100m, AccrualType.Expense, description: "an-expense");
        h.SeedAccrual(Owner, 500m, AccrualType.Income, description: "an-income");
        var task = h.SeedTask(Owner);

        await h.Processor.ProcessAsync(task.Id, new AccrualExportFilter(Type: AccrualType.Income));

        var lines = h.DataLines(h.Reload(task.Id).ResultObjectKey);
        Assert.Single(lines);
        Assert.Contains("an-income", lines[0]);
    }

    [Fact]
    public async Task AmountFilter_BoundsTheExportedRows()
    {
        var h = new Harness(nameof(AmountFilter_BoundsTheExportedRows));
        h.SeedAccrual(Owner, 50m, description: "below");
        h.SeedAccrual(Owner, 150m, description: "within");
        h.SeedAccrual(Owner, 500m, description: "above");
        var task = h.SeedTask(Owner);

        await h.Processor.ProcessAsync(task.Id, new AccrualExportFilter(AmountMin: 100m, AmountMax: 200m));

        var lines = h.DataLines(h.Reload(task.Id).ResultObjectKey);
        Assert.Single(lines);
        Assert.Contains("within", lines[0]);
    }

    [Fact]
    public async Task DateFilter_BoundsTheExportedRows()
    {
        var h = new Harness(nameof(DateFilter_BoundsTheExportedRows));
        h.SeedAccrual(Owner, 10m, date: Now.AddDays(-10), description: "too-old");
        h.SeedAccrual(Owner, 20m, date: Now.AddDays(-2), description: "in-range");
        h.SeedAccrual(Owner, 30m, date: Now.AddDays(5), description: "too-new");
        var task = h.SeedTask(Owner);

        await h.Processor.ProcessAsync(task.Id, new AccrualExportFilter(
            DateFrom: Now.AddDays(-5), DateTo: Now.AddDays(1)));

        var lines = h.DataLines(h.Reload(task.Id).ResultObjectKey);
        Assert.Single(lines);
        Assert.Contains("in-range", lines[0]);
    }

    [Fact]
    public async Task AlreadyDone_IsIdempotentNoOp()
    {
        var h = new Harness(nameof(AlreadyDone_IsIdempotentNoOp));
        h.SeedAccrual(Owner, 100m);
        var task = h.SeedTask(Owner);

        await h.Processor.ProcessAsync(task.Id, new AccrualExportFilter()); // first → Done
        var result = await h.Processor.ProcessAsync(task.Id, new AccrualExportFilter()); // second → no-op

        Assert.Equal(AccrualExportOutcome.Skipped, result.Status);
        Assert.Equal(1, h.Storage.UploadCount); // not re-uploaded
    }

    [Fact]
    public async Task MissingTask_IsSkipped()
    {
        var h = new Harness(nameof(MissingTask_IsSkipped));

        var result = await h.Processor.ProcessAsync(Guid.NewGuid(), new AccrualExportFilter());

        Assert.Equal(AccrualExportOutcome.Skipped, result.Status);
        Assert.Equal(0, h.Storage.UploadCount);
    }

    [Fact]
    public async Task TransientFailure_IsRescheduled_AndKeepsTaskRunning()
    {
        var h = new Harness(nameof(TransientFailure_IsRescheduled_AndKeepsTaskRunning));
        h.SeedAccrual(Owner, 100m);
        var task = h.SeedTask(Owner);
        h.Storage.OnUpload = () => throw new InvalidOperationException("storage exploded");

        var result = await h.Processor.ProcessAsync(task.Id, new AccrualExportFilter());

        Assert.Equal(AccrualExportOutcome.Rescheduled, result.Status);
        Assert.NotNull(result.RetryDelay);
        var saved = h.Reload(task.Id);
        // Still Running so the UI keeps showing the export in progress between attempts.
        Assert.Equal(BackgroundTaskStatus.Running, saved.Status);
        Assert.Equal(1, saved.Attempts);
        Assert.Null(saved.Error);
        Assert.Null(saved.CompletedAt);
    }

    [Fact]
    public async Task ExhaustingAttempts_MarksTaskFailed_WithStandardizedError()
    {
        var h = new Harness(nameof(ExhaustingAttempts_MarksTaskFailed_WithStandardizedError));
        h.SeedAccrual(Owner, 100m);
        var task = h.SeedTask(Owner);
        h.Storage.OnUpload = () => throw new InvalidOperationException("secret infra detail: minio://bucket");

        AccrualExportResult result = new(AccrualExportOutcome.Skipped);
        for (var i = 0; i < BackgroundTask.MaxAttempts; i++)
            result = await h.Processor.ProcessAsync(task.Id, new AccrualExportFilter());

        Assert.Equal(AccrualExportOutcome.Failed, result.Status);
        var saved = h.Reload(task.Id);
        Assert.Equal(BackgroundTaskStatus.Failed, saved.Status);
        Assert.Equal(BackgroundTask.MaxAttempts, saved.Attempts);
        Assert.Equal(Now, saved.CompletedAt);
        // The raw exception text must never reach the user-visible Error.
        Assert.Equal(AccrualExportProcessor.FailureMessage, saved.Error);
        Assert.DoesNotContain("minio", saved.Error!);
        Assert.DoesNotContain("infra detail", saved.Error!);
    }

    [Fact]
    public async Task Retry_OverwritesTheSameObjectKey()
    {
        var h = new Harness(nameof(Retry_OverwritesTheSameObjectKey));
        h.SeedAccrual(Owner, 100m);
        var task = h.SeedTask(Owner);

        // Fail the first upload, then let the retry succeed.
        var attempts = 0;
        h.Storage.OnUpload = () =>
        {
            if (++attempts == 1) throw new InvalidOperationException("first attempt fails");
        };

        var first = await h.Processor.ProcessAsync(task.Id, new AccrualExportFilter());
        var second = await h.Processor.ProcessAsync(task.Id, new AccrualExportFilter());

        Assert.Equal(AccrualExportOutcome.Rescheduled, first.Status);
        Assert.Equal(AccrualExportOutcome.Completed, second.Status);

        var saved = h.Reload(task.Id);
        Assert.Equal(BackgroundTaskStatus.Done, saved.Status);
        // One object only — the retry overwrote the stable key, it did not orphan a new file.
        Assert.Single(h.Storage.Objects);
        Assert.True(h.Storage.Objects.ContainsKey(saved.ResultObjectKey));
    }
}
