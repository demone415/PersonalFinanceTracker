using FinanceTracker.Application.Common.Exceptions;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Application.Features.Export;

/// <summary>
/// Producer side of the async CSV export (T6.2.1): persists a Pending
/// <see cref="BackgroundTask"/> for the caller and queues the background job,
/// returning the job id to poll. The heavy lifting (querying + CSV + upload)
/// happens off-request in <see cref="AccrualExportProcessor"/> — the request
/// never exports inline (ARCHITECTURE.md §async import/export).
/// </summary>
public sealed class AccrualExportService(
    IApplicationDbContext db,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IAccrualExportScheduler scheduler,
    ILogger<AccrualExportService> logger)
{
    public async Task<ExportJobResponse> CreateExportAsync(
        AccrualExportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("Authentication required.");

        var task = new BackgroundTask(userId, BackgroundTaskType.ExportCsv);
        db.BackgroundTasks.Add(task);

        // Commit the task row before enqueuing, so the worker can never pick up
        // the job before the row it reports progress on exists.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        scheduler.Enqueue(task.Id, filter);
        logger.LogInformation("Queued CSV export job {JobId} for user {UserId}.", task.Id, userId);

        return new ExportJobResponse(task.Id);
    }
}
