namespace FinanceTracker.Application.Common.Interfaces;

/// <summary>
/// Thin abstraction over the message bus (Wolverine) so feature services can
/// publish integration messages without depending on the transport directly.
/// Implemented in the Infrastructure layer.
/// </summary>
public interface IMessagePublisher
{
    /// <summary>Publishes <paramref name="message"/> to its configured destination.</summary>
    Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : notnull;
}
