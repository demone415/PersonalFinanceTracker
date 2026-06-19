using System.Text.Json;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Application.Features.Import;

/// <summary>What a single import pass decided, so callers/tests can assert it.</summary>
public enum AccrualImportOutcome
{
    /// <summary>The workbook was parsed, receipts imported, summary stored, task Done.</summary>
    Completed,

    /// <summary>A transient failure; the attempt was counted and a retry should be scheduled.</summary>
    Rescheduled,

    /// <summary>The import failed for good (bad file, or retry budget exhausted); task is Failed.</summary>
    Failed,

    /// <summary>Nothing to do — the task is missing or already terminal (idempotent no-op).</summary>
    Skipped,
}

/// <summary>The outcome of <see cref="AccrualImportProcessor.ProcessAsync"/>.</summary>
public sealed record AccrualImportResult(
    AccrualImportOutcome Status,
    TimeSpan? RetryDelay = null,
    ImportSummary? Summary = null);

/// <summary>
/// The off-request body of an FNS import (Story 6.1), free of Hangfire so the
/// whole flow is unit-testable. It is idempotent (a Done/Failed task is a no-op),
/// runs without an HTTP user — so it bypasses the data-isolation filter and scopes
/// explicitly to the task's owner — parses the stored .xlsx, creates an Expense
/// accrual + a linked receipt with line items for each receipt, skipping any whose
/// (ExternalNumber, INN, Date) already exists for the owner, and stores a JSON
/// summary under the task's object key. A malformed file fails terminally; a
/// transient failure is counted and rescheduled until the retry budget is spent.
/// </summary>
public sealed class AccrualImportProcessor(
    IApplicationDbContext db,
    IUnitOfWork unitOfWork,
    IFnsReceiptParser parser,
    IFileStorage fileStorage,
    TimeProvider timeProvider,
    ILogger<AccrualImportProcessor> logger)
{
    private const string SummaryContentType = "application/json";
    private const string ImportDescriptionFallback = "Импорт ФНС";

    /// <summary>Generic, infra-detail-free message shown to the user on a terminal failure.</summary>
    public const string FailureMessage =
        "Не удалось импортировать файл. Повторите попытку позже.";

    /// <summary>Message shown when the uploaded file is not a readable FNS export.</summary>
    public const string BadFormatMessage =
        "Файл не распознан как выгрузка чеков ФНС (.xlsx). Проверьте формат файла.";

    public async Task<AccrualImportResult> ProcessAsync(
        Guid taskId,
        string sourceObjectKey,
        CancellationToken cancellationToken = default)
    {
        // Background work has no HTTP user, so the data-isolation filter would hide
        // every row — bypass it and load the task by its (unguessable) id.
        var task = await db.BackgroundTasks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task is null)
        {
            logger.LogWarning("Import task {TaskId} not found; nothing to do.", taskId);
            return new(AccrualImportOutcome.Skipped);
        }

        if (task.Status is BackgroundTaskStatus.Done or BackgroundTaskStatus.Failed)
        {
            logger.LogDebug("Import task {TaskId} is {Status}; skipping.", taskId, task.Status);
            return new(AccrualImportOutcome.Skipped);
        }

        if (task.Status == BackgroundTaskStatus.Pending)
        {
            task.Start();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        IReadOnlyList<ParsedReceipt> parsed;
        try
        {
            await using var source = await fileStorage.OpenReadAsync(sourceObjectKey, cancellationToken);
            parsed = parser.Parse(source);
        }
        catch (FnsImportFormatException ex)
        {
            // A bad file will never parse on retry — fail terminally now.
            logger.LogWarning(ex, "Import task {TaskId} rejected: malformed file.", taskId);
            task.Fail(BadFormatMessage, timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new(AccrualImportOutcome.Failed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return await HandleFailureAsync(task, ex, cancellationToken);
        }

        try
        {
            var summary = await ImportAsync(task, parsed, cancellationToken);

            var bytes = JsonSerializer.SerializeToUtf8Bytes(summary, SummaryJsonOptions);
            using (var stream = new MemoryStream(bytes, writable: false))
            {
                await fileStorage.UploadAsync(task.ResultObjectKey, stream, SummaryContentType, cancellationToken);
            }

            task.Complete(timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Import task {TaskId} completed: {Imported} imported, {Skipped} duplicate, {Failed} bad row(s).",
                taskId, summary.ReceiptsImported, summary.ReceiptsSkippedDuplicate, summary.RowsFailed);
            return new(AccrualImportOutcome.Completed, Summary: summary);
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
    /// Creates the accruals + receipts for the parsed workbook, skipping receipts
    /// whose (ExternalNumber, INN, Date) already exist for the owner, and commits
    /// once. Returns the counts for the stored summary.
    /// </summary>
    private async Task<ImportSummary> ImportAsync(
        BackgroundTask task, IReadOnlyList<ParsedReceipt> parsed, CancellationToken cancellationToken)
    {
        var ownerId = task.UserId;

        // Existing dedup keys for this owner (and a per-run set, so duplicate groups
        // inside one file are imported once).
        var existing = await db.Receipts
            .IgnoreQueryFilters()
            .Where(r => r.UserId == ownerId && r.ExternalNumber != null)
            .Select(r => new { r.ExternalNumber, r.INN, r.Date })
            .ToListAsync(cancellationToken);

        var seen = new HashSet<string>(
            existing.Select(e => DedupKey(e.ExternalNumber!, e.INN, e.Date)),
            StringComparer.Ordinal);

        // Categories the owner can use (shared system + their own), matched by name.
        var categories = await db.Categories
            .IgnoreQueryFilters()
            .Where(c => c.IsSystem || c.UserId == ownerId)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);
        var categoryByName = categories
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var imported = 0;
        var skipped = 0;
        var warnings = new List<string>();

        foreach (var pr in parsed)
        {
            var key = DedupKey(pr.ExternalNumber, pr.Inn, pr.Date);
            if (!seen.Add(key))
            {
                skipped++;
                continue;
            }

            var receipt = Receipt.CreateImported(
                ownerId, pr.ExternalNumber, ToKopecks(pr.Total), pr.Date,
                organization: pr.Organization, address: pr.Address, inn: pr.Inn);

            foreach (var item in pr.Items)
                receipt.AddItem(new ReceiptItem(receipt.Id, item.Name, item.Price, item.Quantity, item.Sum));

            Guid? categoryId = pr.Category is { Length: > 0 } && categoryByName.TryGetValue(pr.Category, out var cid)
                ? cid
                : null;

            var description = !string.IsNullOrWhiteSpace(pr.Description) ? pr.Description
                : !string.IsNullOrWhiteSpace(pr.Organization) ? pr.Organization
                : ImportDescriptionFallback;

            var accrual = new Accrual(
                ownerId, pr.Total, pr.Date, AccrualType.Expense,
                currency: "RUB", categoryId: categoryId, description: description, includeInStats: true);
            accrual.SetReceipt(receipt.Id);

            db.Receipts.Add(receipt);
            db.Accruals.Add(accrual);
            imported++;
        }

        task.ReportProgress(90);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ImportSummary(parsed.Count, imported, skipped, RowsFailed: 0, warnings);
    }

    private async Task<AccrualImportResult> HandleFailureAsync(
        BackgroundTask task, Exception ex, CancellationToken cancellationToken)
    {
        task.RecordAttempt();
        var delay = task.GetNextRetryDelay();

        if (delay is null)
        {
            logger.LogError(ex,
                "Import task {TaskId} failed terminally after {Attempts} attempt(s).",
                task.Id, task.Attempts);
            task.Fail(FailureMessage, timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new(AccrualImportOutcome.Failed);
        }

        logger.LogWarning(ex,
            "Import task {TaskId} attempt {Attempts}/{Max} failed; retrying in {Delay}.",
            task.Id, task.Attempts, BackgroundTask.MaxAttempts, delay.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new(AccrualImportOutcome.Rescheduled, delay.Value);
    }

    private static string DedupKey(string externalNumber, string? inn, DateTimeOffset date) =>
        $"{externalNumber.Trim()}|{inn?.Trim() ?? ""}|{date.UtcDateTime:O}";

    private static long ToKopecks(decimal rubles) => (long)Math.Round(rubles * 100m, MidpointRounding.AwayFromZero);

    private static readonly JsonSerializerOptions SummaryJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
