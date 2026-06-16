using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Receipts;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Messaging;

/// <summary>
/// Wolverine consumer for <see cref="ReceiptFetchRequested"/> (T4.2.1): triggers a
/// round-robin dispatch pass on the global FIFO Hangfire queue, which then picks
/// up this (and every other due) receipt fairly across users (T4.2.2). Kept thin
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
            "Receipt {ReceiptId} (user {UserId}) scanned; requesting dispatch pass.",
            message.ReceiptId, message.UserId);

        scheduler.RequestDispatch();
    }
}
