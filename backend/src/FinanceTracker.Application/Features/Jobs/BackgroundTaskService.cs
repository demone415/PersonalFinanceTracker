using System.Globalization;
using FinanceTracker.Application.Common.Exceptions;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Application.Features.Jobs;

/// <summary>
/// Read side of async import/export jobs (T6.1.3 / T6.2.2): status polling and
/// streaming the finished result. Both run in the request scope, so ownership is
/// enforced by the data-isolation global query filter — a regular caller only
/// ever sees their own task (a missing/foreign id is a 404); admins may inspect
/// any. The result is streamed through the API from the private bucket; no
/// presigned URL is ever handed out (ARCHITECTURE.md §async import/export).
/// </summary>
public sealed class BackgroundTaskService(
    IApplicationDbContext db,
    IFileStorage fileStorage)
{
    public async Task<BackgroundTaskStatusDto> GetStatusAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var task = await LoadAsync(id, cancellationToken);
        return ToDto(task);
    }

    public async Task<JobResultStream> OpenResultAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var task = await LoadAsync(id, cancellationToken);

        if (task.Status != BackgroundTaskStatus.Done || string.IsNullOrEmpty(task.ResultObjectKey))
            throw new ConflictException(
                $"Job {id} has no result to download (status: {task.Status}).");

        if (!await fileStorage.ExistsAsync(task.ResultObjectKey, cancellationToken))
            throw new NotFoundException("JobResult", id);

        var content = await fileStorage.OpenReadAsync(task.ResultObjectKey, cancellationToken);
        var fileName = BuildFileName(task);
        return new JobResultStream(content, ContentTypeFor(task.Type), fileName);
    }

    private async Task<BackgroundTask> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await db.BackgroundTasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
        ?? throw new NotFoundException(nameof(BackgroundTask), id);

    private static BackgroundTaskStatusDto ToDto(BackgroundTask task) => new(
        task.Id,
        task.Type.ToString(),
        task.Status.ToString(),
        task.Progress,
        task.Error,
        task.CreatedAt,
        task.CompletedAt,
        // The object key is assigned at creation, so a result is downloadable only
        // once the job is Done — not merely because the key exists.
        task.Status == BackgroundTaskStatus.Done);

    private static string ContentTypeFor(BackgroundTaskType type) => type switch
    {
        BackgroundTaskType.ExportCsv => "text/csv",
        _ => "application/octet-stream",
    };

    private static string BuildFileName(BackgroundTask task)
    {
        var stamp = task.CreatedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return task.Type == BackgroundTaskType.ExportCsv
            ? $"accruals-export-{stamp}.csv"
            : $"job-{task.Id}";
    }
}
