using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Application.Features.Export;

/// <summary>What a single export pass decided, so callers/tests can assert it.</summary>
public enum AccrualExportOutcome
{
    /// <summary>The CSV was generated, stored, and the task marked Done.</summary>
    Completed,

    /// <summary>A transient failure; the attempt was counted and a retry should be scheduled (<see cref="AccrualExportResult.RetryDelay"/>).</summary>
    Rescheduled,

    /// <summary>The export failed for good (retry budget exhausted); the task is marked Failed.</summary>
    Failed,

    /// <summary>Nothing to do — the task is missing or already in a terminal state (idempotent no-op).</summary>
    Skipped,
}

/// <summary>The outcome of <see cref="AccrualExportProcessor.ProcessAsync"/>.</summary>
/// <param name="Status">What happened.</param>
/// <param name="RetryDelay">When <see cref="AccrualExportOutcome.Rescheduled"/>, the wait before the next attempt.</param>
public sealed record AccrualExportResult(
    AccrualExportOutcome Status,
    TimeSpan? RetryDelay = null);

/// <summary>
/// The off-request body of a CSV export (T6.2.1), free of Hangfire so the whole
/// flow is unit-testable. It is idempotent (a Done/Failed task is a no-op, a
/// Pending one is started), runs without an HTTP user — so it bypasses the
/// data-isolation filter and scopes explicitly to the task's owner — applies the
/// requested filters, writes the CSV via <see cref="IAccrualCsvExporter"/>, and
/// uploads it to the private bucket under the task's stable, cryptographically
/// random object key. A transient failure is counted and reported as
/// <see cref="AccrualExportOutcome.Rescheduled"/> (the task stays Running) until
/// the retry budget is spent, after which it is terminally Failed.
/// </summary>
public sealed class AccrualExportProcessor(
    IApplicationDbContext db,
    IUnitOfWork unitOfWork,
    IAccrualCsvExporter csvExporter,
    IFileStorage fileStorage,
    TimeProvider timeProvider,
    ILogger<AccrualExportProcessor> logger)
{
    private const string CsvContentType = "text/csv";

    /// <summary>Generic, infra-detail-free message shown to the user on a terminal failure.</summary>
    public const string FailureMessage =
        "Не удалось сформировать экспорт. Повторите попытку позже.";

    public async Task<AccrualExportResult> ProcessAsync(
        Guid taskId,
        AccrualExportFilter filter,
        CancellationToken cancellationToken = default)
    {
        // Background work has no HTTP user, so the data-isolation filter would hide
        // every row — bypass it and load the task by its (unguessable) id.
        var task = await db.BackgroundTasks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task is null)
        {
            logger.LogWarning("Export task {TaskId} not found; nothing to do.", taskId);
            return new(AccrualExportOutcome.Skipped);
        }

        // Idempotency: a finished task is never reprocessed. A task left Running by
        // a crashed or rescheduled attempt resumes; the upload targets the task's
        // stable object key, so re-running overwrites rather than orphaning a file.
        if (task.Status is BackgroundTaskStatus.Done or BackgroundTaskStatus.Failed)
        {
            logger.LogDebug("Export task {TaskId} is {Status}; skipping.", taskId, task.Status);
            return new(AccrualExportOutcome.Skipped);
        }

        if (task.Status == BackgroundTaskStatus.Pending)
        {
            task.Start();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        try
        {
            var rows = await QueryRowsAsync(task.UserId, filter, cancellationToken);
            var bytes = csvExporter.Export(rows);

            using (var stream = new MemoryStream(bytes, writable: false))
            {
                await fileStorage.UploadAsync(task.ResultObjectKey, stream, CsvContentType, cancellationToken);
            }

            task.Complete(timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Export task {TaskId} completed: {Count} row(s), {Bytes} bytes.",
                taskId, rows.Count, bytes.Length);
            return new(AccrualExportOutcome.Completed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return await HandleFailureAsync(task, ex, cancellationToken);
        }
    }

    /// <summary>
    /// Counts the failed attempt and either reschedules a retry (task stays
    /// Running) or, once the budget is spent, marks the task terminally Failed. The
    /// raw exception is logged but never surfaced to the client — a standardized
    /// message is stored instead (no infra details leak through the status API).
    /// </summary>
    private async Task<AccrualExportResult> HandleFailureAsync(
        Domain.Entities.BackgroundTask task, Exception ex, CancellationToken cancellationToken)
    {
        task.RecordAttempt();
        var delay = task.GetNextRetryDelay();

        if (delay is null)
        {
            logger.LogError(ex,
                "Export task {TaskId} failed terminally after {Attempts} attempt(s).",
                task.Id, task.Attempts);
            task.Fail(FailureMessage, timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new(AccrualExportOutcome.Failed);
        }

        logger.LogWarning(ex,
            "Export task {TaskId} attempt {Attempts}/{Max} failed; retrying in {Delay}.",
            task.Id, task.Attempts, Domain.Entities.BackgroundTask.MaxAttempts, delay.Value);
        // Leave the task Running so the UI keeps showing it in progress between attempts.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new(AccrualExportOutcome.Rescheduled, delay.Value);
    }

    private async Task<IReadOnlyList<AccrualExportRow>> QueryRowsAsync(
        Guid ownerId, AccrualExportFilter filter, CancellationToken cancellationToken)
    {
        var query = db.Accruals
            .IgnoreQueryFilters()
            .Where(a => a.UserId == ownerId);

        if (filter.DateFrom.HasValue)
            query = query.Where(a => a.Date >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue)
            query = query.Where(a => a.Date <= filter.DateTo.Value);
        if (filter.CategoryId.HasValue)
            query = query.Where(a => a.CategoryId == filter.CategoryId.Value);
        if (filter.AmountMin.HasValue)
            query = query.Where(a => a.Amount >= filter.AmountMin.Value);
        if (filter.AmountMax.HasValue)
            query = query.Where(a => a.Amount <= filter.AmountMax.Value);
        if (filter.Type.HasValue)
            query = query.Where(a => a.Type == filter.Type.Value);

        return await query
            .OrderByDescending(a => a.Date)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new AccrualExportRow(
                a.Date,
                a.Type,
                a.Amount,
                a.Currency,
                a.ExchangeRate,
                a.Category != null ? a.Category.Name : null,
                a.Description,
                a.IncludeInStats,
                a.GroupId,
                a.Tags.Select(t => t.Tag).ToList()))
            .ToListAsync(cancellationToken);
    }
}
