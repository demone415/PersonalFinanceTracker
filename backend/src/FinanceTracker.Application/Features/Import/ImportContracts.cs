namespace FinanceTracker.Application.Features.Import;

/// <summary>Response of <c>POST /accruals/import</c> (202 Accepted): the id to poll.</summary>
public sealed record ImportJobResponse(Guid JobId);

/// <summary>
/// The downloadable result of a finished FNS import (stored as JSON under the
/// task's <c>ResultObjectKey</c>): how many receipts were imported, how many were
/// skipped as duplicates, how many source rows could not be parsed, and any
/// non-fatal warnings.
/// </summary>
public sealed record ImportSummary(
    int ReceiptsTotal,
    int ReceiptsImported,
    int ReceiptsSkippedDuplicate,
    int RowsFailed,
    IReadOnlyList<string> Warnings);

/// <summary>
/// One receipt parsed from the FNS Excel export — a group of source rows sharing
/// the same «Номер чека». Receipt-level fields come from the group's first row;
/// <see cref="Items"/> is one entry per source row.
/// </summary>
public sealed record ParsedReceipt(
    string ExternalNumber,
    string? Inn,
    string? Organization,
    string? Address,
    DateTimeOffset Date,
    decimal Total,
    string? Category,
    string? Description,
    IReadOnlyList<ParsedReceiptItem> Items);

/// <summary>One line item of a <see cref="ParsedReceipt"/> (FNS «Товар»/«Цена»/«Количество»/«Сумма по товару»).</summary>
public sealed record ParsedReceiptItem(string Name, decimal Price, decimal Quantity, decimal Sum);

/// <summary>
/// Parses an FNS «Налоги ФЛ» receipts export (.xlsx) into receipts with line
/// items. Implemented in Infrastructure (ClosedXML) so the Application layer
/// stays free of the spreadsheet library. Columns are located by header name, so
/// column reordering is tolerated.
/// </summary>
public interface IFnsReceiptParser
{
    /// <summary>
    /// Reads the workbook from <paramref name="excel"/> and returns one entry per
    /// distinct «Номер чека». Throws <see cref="FnsImportFormatException"/> when the
    /// file is not a readable FNS export (missing required columns / unreadable).
    /// </summary>
    IReadOnlyList<ParsedReceipt> Parse(Stream excel);
}

/// <summary>The uploaded file is not a readable FNS receipts export (terminal — not retried).</summary>
public sealed class FnsImportFormatException(string message) : Exception(message);

/// <summary>
/// Enqueues the FNS import onto the background worker. Implemented in
/// Infrastructure over Hangfire (the Application layer never references Hangfire),
/// mirroring <c>IAccrualExportScheduler</c>.
/// </summary>
public interface IAccrualImportScheduler
{
    /// <summary>Queues the import job for the given (already-persisted) task and stored source file.</summary>
    void Enqueue(Guid taskId, string sourceObjectKey);

    /// <summary>Re-queues the import job after a transient failure, to run once <paramref name="delay"/> has elapsed.</summary>
    void ScheduleRetry(Guid taskId, string sourceObjectKey, TimeSpan delay);
}

/// <summary>Hangfire queue name for async FNS imports (Epic 6).</summary>
public static class ImportQueues
{
    /// <summary>
    /// The queue FNS imports run on. Separate from the strictly sequential
    /// single-worker <c>receipts</c> queue so an import never blocks receipt fetching.
    /// </summary>
    public const string Imports = "imports";
}
