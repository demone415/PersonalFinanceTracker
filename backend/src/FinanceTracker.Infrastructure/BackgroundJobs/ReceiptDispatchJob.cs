using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Receipts;
using FinanceTracker.Domain.Enums;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.BackgroundJobs;

/// <summary>
/// Recurring fairness/recovery sweep on the FIFO <c>receipts</c> queue (T4.2.2).
/// It finds every Pending receipt whose next attempt is due, orders them
/// round-robin across users so no one monopolises the single worker, and runs
/// each through the processor in turn. It also recovers receipts orphaned by a
/// lost message or an app restart. Running on the single-worker queue means it
/// never overlaps a per-receipt <see cref="ReceiptFetchJob"/>, and the processor
/// is idempotent, so any overlap with a scheduled retry is a safe no-op.
/// </summary>
[Queue(ReceiptQueues.Hangfire)]
public sealed class ReceiptDispatchJob(
    IApplicationDbContext db,
    ReceiptFetchProcessor processor,
    TimeProvider timeProvider,
    ILogger<ReceiptDispatchJob> logger)
{
    /// <summary>Cap on receipts handled per sweep; the daily quota keeps the real count tiny.</summary>
    private const int BatchLimit = 100;

    public async Task DispatchAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var due = await db.Receipts
            .IgnoreQueryFilters()
            .Where(r => r.FetchStatus == ReceiptFetchStatus.Pending
                        && (r.NextFetchAt == null || r.NextFetchAt <= now))
            .OrderBy(r => r.NextFetchAt ?? DateTimeOffset.MinValue)
            .ThenBy(r => r.Id)
            .Take(BatchLimit)
            .Select(r => new DueReceipt(r.Id, r.UserId))
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return;
        }

        var ordered = RoundRobinReceiptOrdering.Interleave(due, d => d.UserId);
        logger.LogInformation("Dispatching {Count} due receipt(s) round-robin.", ordered.Count);

        foreach (var item in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await processor.ProcessAsync(item.ReceiptId, cancellationToken);
            if (result.Status == ReceiptFetchProcessingStatus.Paused)
            {
                // Rate-limiter down → fail-closed: stop the whole sweep (ARCHITECTURE.md §4).
                logger.LogWarning("Rate-limiter unavailable; pausing the dispatch sweep.");
                break;
            }
        }
    }

    private sealed record DueReceipt(Guid ReceiptId, Guid UserId);
}
