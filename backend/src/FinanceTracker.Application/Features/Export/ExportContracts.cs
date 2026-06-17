using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Features.Export;

/// <summary>
/// The accrual filters carried into an async CSV export (T6.2.1). Mirrors the
/// list filters (<c>AccrualFilterRequest</c>) minus pagination — an export covers
/// every matching row, not a page. Kept as a flat, primitive record so it
/// survives serialization as a Hangfire job argument.
/// </summary>
public sealed record AccrualExportFilter(
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null,
    Guid? CategoryId = null,
    decimal? AmountMin = null,
    decimal? AmountMax = null,
    AccrualType? Type = null);

/// <summary>Response of <c>POST /accruals/export</c> (202 Accepted): the id to poll.</summary>
public sealed record ExportJobResponse(Guid JobId);

/// <summary>
/// Enqueues the CSV export onto the background worker. Implemented in
/// Infrastructure over Hangfire (the Application layer never references Hangfire),
/// mirroring <c>IReceiptFetchScheduler</c>.
/// </summary>
public interface IAccrualExportScheduler
{
    /// <summary>Queues the export job for the given (already-persisted) task.</summary>
    void Enqueue(Guid taskId, AccrualExportFilter filter);

    /// <summary>
    /// Re-queues the export job after a transient failure, to run once
    /// <paramref name="delay"/> has elapsed (the job stays on the <c>exports</c>
    /// queue). Mirrors the receipt-fetch reschedule scheme.
    /// </summary>
    void ScheduleRetry(Guid taskId, AccrualExportFilter filter, TimeSpan delay);
}

/// <summary>Hangfire queue names for async import/export jobs (Epic 6).</summary>
public static class ExportQueues
{
    /// <summary>
    /// The queue CSV exports run on. Deliberately separate from the strictly
    /// sequential single-worker <c>receipts</c> queue so a long export never
    /// blocks receipt fetching (and exports may run in parallel).
    /// </summary>
    public const string Exports = "exports";
}
