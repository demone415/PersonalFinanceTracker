using FinanceTracker.Application.Common.Interfaces;
using Wolverine;

namespace FinanceTracker.Infrastructure.Messaging;

/// <summary><see cref="IMessagePublisher"/> over the Wolverine message bus.</summary>
internal sealed class WolverineMessagePublisher(IMessageBus bus) : IMessagePublisher
{
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : notnull =>
        bus.PublishAsync(message).AsTask();
}
