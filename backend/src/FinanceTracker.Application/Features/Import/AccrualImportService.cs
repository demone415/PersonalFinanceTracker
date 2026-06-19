using FinanceTracker.Application.Common.Exceptions;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Application.Features.Import;

/// <summary>
/// Producer side of the async FNS import (Story 6.1): stores the uploaded .xlsx in
/// the private bucket, persists a Pending <see cref="BackgroundTask"/> for the
/// caller and queues the background job, returning the job id to poll. Parsing,
/// dedup and persistence happen off-request in <see cref="AccrualImportProcessor"/>
/// — the request never imports inline (ARCHITECTURE.md §async import/export).
/// </summary>
public sealed class AccrualImportService(
    IApplicationDbContext db,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IFileStorage fileStorage,
    IAccrualImportScheduler scheduler,
    ILogger<AccrualImportService> logger)
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<ImportJobResponse> CreateImportAsync(
        Stream file,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("Authentication required.");

        if (file is null || (file.CanSeek && file.Length == 0))
            throw new ValidationException("Файл импорта пуст.");

        var task = new BackgroundTask(userId, BackgroundTaskType.ImportFns);

        // Store the source workbook under a private, task-scoped key the job reads
        // off-request. It is never exposed to the client (no presigned URL).
        var sourceObjectKey = $"imports/src/{task.Id:N}.xlsx";
        await fileStorage.UploadAsync(sourceObjectKey, file, XlsxContentType, cancellationToken);

        db.BackgroundTasks.Add(task);

        // Commit the task row before enqueuing, so the worker can never pick up the
        // job before the row it reports progress on exists.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        scheduler.Enqueue(task.Id, sourceObjectKey);
        logger.LogInformation("Queued FNS import job {JobId} for user {UserId}.", task.Id, userId);

        return new ImportJobResponse(task.Id);
    }
}
