using System.Diagnostics.Metrics;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Api.Observability;

/// <summary>
/// Publishes the receipt-pipeline gauges required by ARCHITECTURE.md §11.7:
/// the background queue length (Pending receipts awaiting a fetch) and the
/// remaining global provider quota for the day. Observable-gauge callbacks must
/// be synchronous, so a background loop refreshes cached values that the gauges
/// read; a probe failure keeps the last value rather than reporting a wrong one.
/// </summary>
internal sealed class ReceiptQueueMetrics : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReceiptQueueMetrics> _logger;

    // -1 sentinels mean "not measured yet / probe unavailable" — not emitted as 0.
    private long _pendingReceipts = -1;
    private long _remainingProviderQuota = -1;

    public ReceiptQueueMetrics(
        IServiceScopeFactory scopeFactory,
        IMeterFactory meterFactory,
        ILogger<ReceiptQueueMetrics> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var meter = meterFactory.Create(ObservabilityExtensions.MeterName);

        meter.CreateObservableGauge(
            "finance.receipts.pending",
            () => Measure(_pendingReceipts),
            unit: "{receipts}",
            description: "Receipts queued (Pending) awaiting a background fetch.");

        meter.CreateObservableGauge(
            "finance.receipts.provider_quota_remaining",
            () => Measure(_remainingProviderQuota),
            unit: "{requests}",
            description: "Remaining ProverkaCheka requests in the global daily quota.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            await RefreshAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var rateLimiter = scope.ServiceProvider.GetRequiredService<IReceiptRateLimiter>();

            // Global queue depth across all users (background context → bypass isolation).
            _pendingReceipts = await db.Receipts
                .IgnoreQueryFilters()
                .CountAsync(r => r.FetchStatus == ReceiptFetchStatus.Pending, cancellationToken);

            var remaining = await rateLimiter.GetRemainingGlobalQuotaAsync(cancellationToken);
            _remainingProviderQuota = remaining ?? -1; // limiter unavailable → keep sentinel
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Metrics are best-effort: never let a probe failure crash the host or
            // emit a misleading value — keep the previous reading.
            _logger.LogDebug(ex, "Receipt metrics refresh failed; keeping last values.");
        }
    }

    private static IEnumerable<Measurement<long>> Measure(long value) =>
        value < 0 ? [] : [new Measurement<long>(value)];
}
