using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Receipts;
using FinanceTracker.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;
using Wolverine;

namespace FinanceTracker.IntegrationTests;

/// <summary>
/// Wolverine producer→consumer round-trip over a real RabbitMQ (Testcontainers),
/// as required by CLAUDE.md. Publishing <see cref="ReceiptFetchRequested"/> must
/// reach <see cref="ReceiptFetchRequestedHandler"/> (handler discovery + the
/// transport routing in <see cref="WolverineConfiguration"/>), which then asks the
/// scheduler for a dispatch pass.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ReceiptFetchMessagingTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:3-management")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    private string? _startupError;

    public async Task InitializeAsync()
    {
        try
        {
            await _rabbit.StartAsync();
        }
        catch (Exception ex)
        {
            _startupError = ex.Message;
        }
    }

    public async Task DisposeAsync() => await _rabbit.DisposeAsync();

    /// <summary>Captures the consumer's call so the test can await the round-trip.</summary>
    private sealed class RecordingScheduler : IReceiptFetchScheduler
    {
        private readonly TaskCompletionSource _dispatched = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void RequestDispatch() => _dispatched.TrySetResult();

        public void ScheduleRetry(Guid receiptId, TimeSpan delay) { }

        public Task WaitForDispatchAsync(TimeSpan timeout) => _dispatched.Task.WaitAsync(timeout);
    }

    [SkippableFact]
    public async Task PublishedReceiptFetchRequested_ReachesHandler_AndRequestsDispatch()
    {
        Skip.If(_startupError is not null, $"RabbitMQ container unavailable: {_startupError}");

        var recorder = new RecordingScheduler();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:Host"] = _rabbit.Hostname,
            ["RabbitMq:Port"] = _rabbit.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "guest",
            ["RabbitMq:Password"] = "guest",
        });
        builder.Services.AddSingleton<IReceiptFetchScheduler>(recorder);
        builder.UseWolverine(options => WolverineConfiguration.Configure(options, builder.Configuration));

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var bus = host.Services.GetRequiredService<IMessageBus>();
            await bus.PublishAsync(new ReceiptFetchRequested(Guid.NewGuid(), Guid.NewGuid(), "t=1&s=1&fn=1&i=1&fp=1&n=1"));

            // Throws TimeoutException (failing the test) if the message never arrives.
            await recorder.WaitForDispatchAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            await host.StopAsync();
        }
    }
}
