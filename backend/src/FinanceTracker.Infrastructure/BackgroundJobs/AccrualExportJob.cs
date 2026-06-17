using FinanceTracker.Application.Features.Export;
using Hangfire;

namespace FinanceTracker.Infrastructure.BackgroundJobs;

/// <summary>
/// The Hangfire entry point for a CSV export (T6.2.1). It runs on the dedicated
/// <c>exports</c> queue — separate from the strictly sequential single-worker
/// <c>receipts</c> queue — and delegates to the transport-agnostic
/// <see cref="AccrualExportProcessor"/>, which owns the (idempotent) flow. On a
/// transient failure the processor reports <see cref="AccrualExportOutcome.Rescheduled"/>
/// and this job re-enqueues a delayed retry, mirroring the receipt-fetch scheme
/// (the processor counts attempts and goes terminally Failed once the budget is
/// spent, so the retry loop is bounded).
/// </summary>
[Queue(ExportQueues.Exports)]
public sealed class AccrualExportJob(
    AccrualExportProcessor processor,
    IAccrualExportScheduler scheduler)
{
    public async Task RunAsync(Guid taskId, AccrualExportFilter filter, CancellationToken cancellationToken)
    {
        var result = await processor.ProcessAsync(taskId, filter, cancellationToken);

        if (result.Status == AccrualExportOutcome.Rescheduled && result.RetryDelay is { } delay)
        {
            scheduler.ScheduleRetry(taskId, filter, delay);
        }
    }
}
