using FinanceTracker.Application.Features.Import;
using Hangfire;

namespace FinanceTracker.Infrastructure.BackgroundJobs;

/// <summary>
/// The Hangfire entry point for an FNS import (Story 6.1). It runs on the dedicated
/// <c>imports</c> queue — separate from the strictly sequential single-worker
/// <c>receipts</c> queue — and delegates to the transport-agnostic
/// <see cref="AccrualImportProcessor"/>, which owns the (idempotent) flow. On a
/// transient failure the processor reports <see cref="AccrualImportOutcome.Rescheduled"/>
/// and this job re-enqueues a delayed retry; the processor counts attempts and goes
/// terminally Failed once the budget is spent, so the retry loop is bounded.
/// </summary>
[Queue(ImportQueues.Imports)]
public sealed class AccrualImportJob(
    AccrualImportProcessor processor,
    IAccrualImportScheduler scheduler)
{
    public async Task RunAsync(Guid taskId, string sourceObjectKey, CancellationToken cancellationToken)
    {
        var result = await processor.ProcessAsync(taskId, sourceObjectKey, cancellationToken);

        if (result.Status == AccrualImportOutcome.Rescheduled && result.RetryDelay is { } delay)
        {
            scheduler.ScheduleRetry(taskId, sourceObjectKey, delay);
        }
    }
}
