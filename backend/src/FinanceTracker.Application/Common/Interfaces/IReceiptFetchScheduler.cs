namespace FinanceTracker.Application.Common.Interfaces;

/// <summary>
/// Enqueues receipt-fetch work onto the global FIFO background queue
/// (Hangfire, <c>WorkerCount = 1</c>). Implemented in Infrastructure; the
/// abstraction keeps the Wolverine consumer and the processor free of Hangfire.
/// </summary>
public interface IReceiptFetchScheduler
{
    /// <summary>
    /// Triggers a dispatch pass over all currently-due receipts. New scans enter
    /// through here (not via a direct per-receipt enqueue) so the single worker
    /// processes users round-robin (T4.2.2) instead of in raw arrival order.
    /// </summary>
    void RequestDispatch();

    /// <summary>
    /// Schedules the next attempt for one receipt after <paramref name="delay"/>
    /// (the retry scheme, T4.2.3). A single rescheduled receipt needs precise
    /// timing, not fairness, so it is queued directly.
    /// </summary>
    void ScheduleRetry(Guid receiptId, TimeSpan delay);
}
