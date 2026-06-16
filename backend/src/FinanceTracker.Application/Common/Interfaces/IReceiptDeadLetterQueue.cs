namespace FinanceTracker.Application.Common.Interfaces;

/// <summary>
/// Sink for receipts that fail terminally. The Infrastructure implementation
/// publishes a dead-letter message to the DLQ for inspection; the receipt is
/// already persisted as <c>Failed</c>/<c>RetryLimit</c> by the processor.
/// </summary>
public interface IReceiptDeadLetterQueue
{
    Task SendAsync(Guid receiptId, string reason, CancellationToken cancellationToken = default);
}
