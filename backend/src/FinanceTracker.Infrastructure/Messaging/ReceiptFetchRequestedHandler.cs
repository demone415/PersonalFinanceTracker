using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Receipts;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Messaging;

/// <summary>
/// Wolverine consumer for <see cref="ReceiptFetchRequested"/> (T4.2.1): hands the
/// receipt to the global FIFO Hangfire queue for sequential processing. Kept thin
/// so a failure here is rare; on repeated failure Wolverine retries and then
/// dead-letters the message (see the transport configuration).
/// </summary>
public sealed class ReceiptFetchRequestedHandler
{
    public void Handle(
        ReceiptFetchRequested message,
        IReceiptFetchScheduler scheduler,
        ILogger<ReceiptFetchRequestedHandler> logger)
    {
        logger.LogInformation(
            "Queueing receipt {ReceiptId} (user {UserId}) for background fetch.",
            message.ReceiptId, message.UserId);

        scheduler.Enqueue(message.ReceiptId);
    }
}
