using FinanceTracker.Application.Features.Receipts;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.RabbitMQ;

namespace FinanceTracker.Infrastructure.Messaging;

/// <summary>
/// Wolverine message-bus configuration (Story 4.2): the RabbitMQ transport, the
/// receipt-fetch request listener, explicit routing, and the dead-letter policy.
/// Invoked from the API host (<c>UseWolverine</c>); kept in Infrastructure so the
/// API stays free of transport details.
/// </summary>
public static class WolverineConfiguration
{
    public static void Configure(WolverineOptions options, IConfiguration configuration)
    {
        var rabbit = configuration.GetSection("RabbitMq");

        options.UseRabbitMq(factory =>
            {
                factory.HostName = rabbit["Host"] ?? "localhost";
                factory.Port = int.TryParse(rabbit["Port"], out var port) ? port : 5672;
                factory.UserName = rabbit["Username"] ?? "guest";
                factory.Password = rabbit["Password"] ?? "guest";
            })
            // Declare the queues/exchanges (and their dead-letter queues) at startup.
            .AutoProvision();

        // Message handlers (e.g. ReceiptFetchRequestedHandler) live in this assembly.
        options.Discovery.IncludeAssembly(typeof(WolverineConfiguration).Assembly);

        // Inbound: the receipt-fetch request queue → ReceiptFetchRequestedHandler.
        options.ListenToRabbitQueue(ReceiptQueues.FetchRequests);

        // Outbound routing: requests go to the work queue; terminal failures to the DLQ.
        options.PublishMessage<ReceiptFetchRequested>().ToRabbitQueue(ReceiptQueues.FetchRequests);
        options.PublishMessage<ReceiptFetchDeadLettered>().ToRabbitQueue(ReceiptQueues.DeadLetter);

        // Terminal/poison handling (T4.2.1): a few spaced retries, after which
        // Wolverine moves the envelope to the dead-letter queue automatically.
        options.Policies.OnException<Exception>()
            .RetryWithCooldown(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15));
    }
}
