using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Receipts;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.BackgroundJobs;

/// <summary>
/// The per-receipt unit of work on the global FIFO <c>receipts</c> queue
/// (T4.2.3). Delegates the decision to <see cref="ReceiptFetchProcessor"/> and
/// then acts on the result: schedules the next attempt per the retry scheme, or
/// retries shortly when the rate-limiter is temporarily down. The job is
/// idempotent (the processor no-ops a non-Pending receipt), so a duplicate
/// enqueue or a retry racing the dispatcher is harmless.
/// </summary>
[Queue(ReceiptQueues.Hangfire)]
public sealed class ReceiptFetchJob(
    ReceiptFetchProcessor processor,
    IReceiptFetchScheduler scheduler,
    ILogger<ReceiptFetchJob> logger)
{
    /// <summary>How long to wait before retrying when the rate-limiter store is unreachable.</summary>
    private static readonly TimeSpan LimiterRecoveryDelay = TimeSpan.FromMinutes(5);

    public async Task RunAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        var result = await processor.ProcessAsync(receiptId, cancellationToken);

        switch (result.Status)
        {
            case ReceiptFetchProcessingStatus.Rescheduled:
            case ReceiptFetchProcessingStatus.Deferred:
                if (result.RetryDelay is { } delay)
                {
                    scheduler.ScheduleRetry(receiptId, delay);
                }

                break;

            case ReceiptFetchProcessingStatus.Paused:
                // Fail-closed: the limiter is down, so come back once it likely recovered.
                logger.LogWarning(
                    "Receipt {ReceiptId} paused (rate-limiter down); retrying in {Delay}.",
                    receiptId, LimiterRecoveryDelay);
                scheduler.ScheduleRetry(receiptId, LimiterRecoveryDelay);
                break;

            case ReceiptFetchProcessingStatus.Completed:
            case ReceiptFetchProcessingStatus.DeadLettered:
            case ReceiptFetchProcessingStatus.Skipped:
            default:
                break;
        }
    }
}
