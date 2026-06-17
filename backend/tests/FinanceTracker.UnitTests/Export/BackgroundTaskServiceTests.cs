using System.Text;
using FinanceTracker.Application.Common.Exceptions;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Jobs;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.UnitTests.Export;

/// <summary>
/// The read side of async jobs (T6.1.3 / T6.2.2): status polling and streaming the
/// finished result. Ownership is enforced by the data-isolation global query
/// filter, so these run under a real (non-admin) caller against an in-memory
/// database — a foreign/missing id is a 404, the result streams only once the job
/// is Done, and the file is served through the service (never a presigned URL).
/// </summary>
public class BackgroundTaskServiceTests
{
    private static readonly Guid Owner = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Other = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    private sealed record StubCurrentUser(Guid? UserId, bool IsAdmin) : ICurrentUserService;

    private sealed class FakeFileStorage : IFileStorage
    {
        public readonly Dictionary<string, byte[]> Objects = new();

        public Task UploadAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            Objects[objectKey] = ms.ToArray();
            return Task.CompletedTask;
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
        public BackgroundTaskService Service { get; }

        public Harness(string dbName, Guid caller, bool isAdmin = false)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            Db = new AppDbContext(options, new StubCurrentUser(caller, isAdmin));
            Service = new BackgroundTaskService(Db, Storage);
        }

        /// <summary>Seeds a task in the given state; for Done it also writes the object to storage.</summary>
        public BackgroundTask SeedTask(Guid userId, BackgroundTaskStatus status, bool withObject = true)
        {
            var task = new BackgroundTask(userId, BackgroundTaskType.ExportCsv);
            if (status is BackgroundTaskStatus.Running or BackgroundTaskStatus.Done)
                task.Start();
            if (status == BackgroundTaskStatus.Done)
            {
                task.Complete(Now);
                if (withObject)
                    Storage.Objects[task.ResultObjectKey] = Encoding.UTF8.GetBytes("Date,Amount\r\n");
            }

            Db.BackgroundTasks.Add(task);
            Db.SaveChanges();
            return task;
        }
    }

    [Fact]
    public async Task GetStatus_ReturnsDto_ForOwnDoneJob()
    {
        var h = new Harness(nameof(GetStatus_ReturnsDto_ForOwnDoneJob), Owner);
        var task = h.SeedTask(Owner, BackgroundTaskStatus.Done);

        var dto = await h.Service.GetStatusAsync(task.Id);

        Assert.Equal(task.Id, dto.Id);
        Assert.Equal("ExportCsv", dto.Type);
        Assert.Equal("Done", dto.Status);
        Assert.Equal(100, dto.Progress);
        Assert.True(dto.HasResult);
        // The opaque object key is never part of the public status.
    }

    [Fact]
    public async Task GetStatus_PendingJob_HasNoResult()
    {
        var h = new Harness(nameof(GetStatus_PendingJob_HasNoResult), Owner);
        var task = h.SeedTask(Owner, BackgroundTaskStatus.Pending);

        var dto = await h.Service.GetStatusAsync(task.Id);

        Assert.Equal("Pending", dto.Status);
        Assert.False(dto.HasResult); // key exists from creation, but the result isn't ready until Done
    }

    [Fact]
    public async Task GetStatus_ForeignJob_IsNotFound()
    {
        var h = new Harness(nameof(GetStatus_ForeignJob_IsNotFound), Owner);
        var foreign = h.SeedTask(Other, BackgroundTaskStatus.Done);

        // Filtered out by data isolation — indistinguishable from a missing id.
        await Assert.ThrowsAsync<NotFoundException>(() => h.Service.GetStatusAsync(foreign.Id));
    }

    [Fact]
    public async Task GetStatus_MissingJob_IsNotFound()
    {
        var h = new Harness(nameof(GetStatus_MissingJob_IsNotFound), Owner);

        await Assert.ThrowsAsync<NotFoundException>(() => h.Service.GetStatusAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task OpenResult_StreamsTheFile_ForOwnDoneJob()
    {
        var h = new Harness(nameof(OpenResult_StreamsTheFile_ForOwnDoneJob), Owner);
        var task = h.SeedTask(Owner, BackgroundTaskStatus.Done);

        var result = await h.Service.OpenResultAsync(task.Id);

        Assert.Equal("text/csv", result.ContentType);
        Assert.StartsWith("accruals-export-", result.FileName);
        Assert.EndsWith(".csv", result.FileName);
        // The caller owns the stream and disposes it (the StreamReader does so here).
        using var reader = new StreamReader(result.Content);
        Assert.Contains("Date,Amount", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task OpenResult_NotDoneJob_IsConflict()
    {
        var h = new Harness(nameof(OpenResult_NotDoneJob_IsConflict), Owner);
        var task = h.SeedTask(Owner, BackgroundTaskStatus.Running);

        await Assert.ThrowsAsync<ConflictException>(() => h.Service.OpenResultAsync(task.Id));
    }

    [Fact]
    public async Task OpenResult_ForeignJob_IsNotFound()
    {
        var h = new Harness(nameof(OpenResult_ForeignJob_IsNotFound), Owner);
        var foreign = h.SeedTask(Other, BackgroundTaskStatus.Done);

        await Assert.ThrowsAsync<NotFoundException>(() => h.Service.OpenResultAsync(foreign.Id));
    }

    [Fact]
    public async Task OpenResult_DoneButObjectMissing_IsNotFound()
    {
        var h = new Harness(nameof(OpenResult_DoneButObjectMissing_IsNotFound), Owner);
        // Done task whose backing object is gone from storage (e.g. lifecycle-expired).
        var task = h.SeedTask(Owner, BackgroundTaskStatus.Done, withObject: false);

        await Assert.ThrowsAsync<NotFoundException>(() => h.Service.OpenResultAsync(task.Id));
    }

    [Fact]
    public async Task Admin_CanInspectAnyJob()
    {
        var h = new Harness(nameof(Admin_CanInspectAnyJob), caller: Guid.NewGuid(), isAdmin: true);
        var task = h.SeedTask(Other, BackgroundTaskStatus.Done);

        var dto = await h.Service.GetStatusAsync(task.Id);

        Assert.Equal("Done", dto.Status); // admin bypasses the data-isolation filter
    }
}
