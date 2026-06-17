using System.Security.Cryptography;
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

    /// <summary>The export threw; the task is marked Failed with the error.</summary>
    Failed,

    /// <summary>Nothing to do — the task is missing or already in a terminal state (idempotent no-op).</summary>
    Skipped,
}

/// <summary>
/// The off-request body of a CSV export (T6.2.1), free of Hangfire so the whole
/// flow is unit-testable. It is idempotent (a Done/Failed task is a no-op, a
/// Pending one is started), runs without an HTTP user — so it bypasses the
/// data-isolation filter and scopes explicitly to the task's owner — applies the
/// requested filters, writes the CSV via <see cref="IAccrualCsvExporter"/>, and
/// uploads it to the private bucket under a cryptographically random opaque key.
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

    public async Task<AccrualExportOutcome> ProcessAsync(
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
            return AccrualExportOutcome.Skipped;
        }

        // Idempotency: a finished task is never reprocessed. A task left Running by
        // a crashed attempt resumes (the upload is deterministic and overwrites).
        if (task.Status is BackgroundTaskStatus.Done or BackgroundTaskStatus.Failed)
        {
            logger.LogDebug("Export task {TaskId} is {Status}; skipping.", taskId, task.Status);
            return AccrualExportOutcome.Skipped;
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

            var objectKey = NewObjectKey();
            using (var stream = new MemoryStream(bytes, writable: false))
            {
                await fileStorage.UploadAsync(objectKey, stream, CsvContentType, cancellationToken);
            }

            task.Complete(objectKey, timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Export task {TaskId} completed: {Count} row(s), {Bytes} bytes.",
                taskId, rows.Count, bytes.Length);
            return AccrualExportOutcome.Completed;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Export task {TaskId} failed.", taskId);
            task.Fail(Truncate(ex.Message, 1000), timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return AccrualExportOutcome.Failed;
        }
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

    /// <summary>
    /// A cryptographically random, opaque 256-bit object key (URL-safe Base64, no
    /// padding). The key — never the GUID id — is what makes the stored file
    /// unguessable (CLAUDE.md: ids are enumerable and not secrets).
    /// </summary>
    private static string NewObjectKey()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return $"exports/{token}.csv";
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
