namespace FinanceTracker.Application.Features.Receipts;

/// <summary>
/// Names of the messaging/queue endpoints used by the receipt-fetch pipeline so
/// the producer, the Wolverine transport and the Hangfire worker agree on them.
/// </summary>
public static class ReceiptQueues
{
    /// <summary>RabbitMQ queue the <see cref="ReceiptFetchRequested"/> message is delivered on.</summary>
    public const string FetchRequests = "receipts-fetch-requests";

    /// <summary>RabbitMQ queue terminal failures are published to for observability (DLQ).</summary>
    public const string DeadLetter = "receipts-dlq";

    /// <summary>
    /// The single global FIFO Hangfire queue receipt fetches run on
    /// (<c>WorkerCount = 1</c> — strictly sequential, never parallel).
    /// </summary>
    public const string Hangfire = "receipts";
}

/// <summary>
/// Published (via Wolverine) when a scanned QR receipt needs to be fetched from
/// the provider. The consumer hands it to the global FIFO Hangfire queue; the
/// <see cref="QrRaw"/> is also persisted on the receipt, so delivery is a
/// best-effort "wake up now" — a lost message is recovered by the dispatcher.
/// </summary>
public sealed record ReceiptFetchRequested(Guid ReceiptId, Guid UserId, string QrRaw);

/// <summary>
/// Published when a receipt fetch fails terminally (invalid receipt, the
/// provider's own retry limit, or our retry budget exhausted). Routed to the
/// dead-letter queue for inspection; the receipt itself is already marked
/// <c>Failed</c>/<c>RetryLimit</c>.
/// </summary>
public sealed record ReceiptFetchDeadLettered(Guid ReceiptId, string Reason);
