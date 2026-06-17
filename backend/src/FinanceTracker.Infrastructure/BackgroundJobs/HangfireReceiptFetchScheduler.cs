using FinanceTracker.Application.Common.Interfaces;
using Hangfire;

namespace FinanceTracker.Infrastructure.BackgroundJobs;

/// <summary>
/// <see cref="IReceiptFetchScheduler"/> backed by Hangfire. Jobs are routed to the
/// global FIFO <c>receipts</c> queue via the <c>[Queue]</c> attribute on
/// <see cref="ReceiptFetchJob"/>; that queue is served by a single worker, so
/// processing is strictly sequential (T4.2.2).
/// </summary>
public sealed class HangfireReceiptFetchScheduler(IBackgroundJobClient client) : IReceiptFetchScheduler
{
    public void RequestDispatch() =>
        client.Enqueue<ReceiptDispatchJob>(job => job.DispatchAsync(CancellationToken.None));

    public void ScheduleRetry(Guid receiptId, TimeSpan delay) =>
        client.Schedule<ReceiptFetchJob>(job => job.RunAsync(receiptId, CancellationToken.None), delay);
}
