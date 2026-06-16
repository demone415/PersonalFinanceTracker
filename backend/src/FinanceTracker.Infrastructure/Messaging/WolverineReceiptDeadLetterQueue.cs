using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Receipts;
using Wolverine;

namespace FinanceTracker.Infrastructure.Messaging;

/// <summary>
/// Publishes a <see cref="ReceiptFetchDeadLettered"/> message to the dead-letter
/// queue when a receipt fails terminally (T4.2.4). The transport routes it to
/// <see cref="ReceiptQueues.DeadLetter"/> per the Wolverine configuration.
/// </summary>
internal sealed class WolverineReceiptDeadLetterQueue(IMessageBus bus) : IReceiptDeadLetterQueue
{
    public Task SendAsync(Guid receiptId, string reason, CancellationToken cancellationToken = default) =>
        bus.PublishAsync(new ReceiptFetchDeadLettered(receiptId, reason)).AsTask();
}
