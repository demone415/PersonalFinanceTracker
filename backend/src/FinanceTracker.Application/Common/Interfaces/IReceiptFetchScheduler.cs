namespace FinanceTracker.Application.Common.Interfaces;

/// <summary>
/// Enqueues receipt-fetch work onto the global FIFO background queue
/// (Hangfire, <c>WorkerCount = 1</c>). Implemented in Infrastructure; the
/// abstraction keeps the Wolverine consumer and the processor free of Hangfire.
/// </summary>
public interface IReceiptFetchScheduler
{
    /// <summary>Queues an immediate fetch attempt for the receipt.</summary>
    void Enqueue(Guid receiptId);

    /// <summary>Schedules the next fetch attempt after <paramref name="delay"/> (retry scheme).</summary>
    void ScheduleRetry(Guid receiptId, TimeSpan delay);
}
