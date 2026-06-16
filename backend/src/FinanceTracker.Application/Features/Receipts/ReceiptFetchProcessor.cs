using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Application.Features.Receipts;

/// <summary>What a single processing pass decided, so the worker can act on it.</summary>
public enum ReceiptFetchProcessingStatus
{
    /// <summary>The receipt was fetched and stored — done.</summary>
    Completed,

    /// <summary>A transient outcome; the next attempt is scheduled (<see cref="ReceiptFetchProcessingResult.RetryDelay"/>).</summary>
    Rescheduled,

    /// <summary>Daily quota spent; deferred until the quota resets (<see cref="ReceiptFetchProcessingResult.RetryDelay"/>).</summary>
    Deferred,

    /// <summary>The rate-limiter store is unreachable; fail-closed — pause and retry later.</summary>
    Paused,

    /// <summary>Terminal failure — the receipt is marked Failed/RetryLimit and dead-lettered.</summary>
    DeadLettered,

    /// <summary>Nothing to do (receipt missing, already resolved, or no QR) — idempotent no-op.</summary>
    Skipped,
}

/// <summary>The outcome of <see cref="ReceiptFetchProcessor.ProcessAsync"/>.</summary>
/// <param name="Status">What happened.</param>
/// <param name="RetryDelay">When <see cref="ReceiptFetchProcessingStatus.Rescheduled"/>/<see cref="ReceiptFetchProcessingStatus.Deferred"/>, the wait before the next attempt.</param>
public sealed record ReceiptFetchProcessingResult(
    ReceiptFetchProcessingStatus Status,
    TimeSpan? RetryDelay = null);

/// <summary>
/// The heart of background receipt fetching (T4.2.3/T4.2.4): processes exactly one
/// receipt, idempotently. It enforces the daily quota (fail-closed when the
/// limiter is down), calls the provider, and turns the provider outcome into a
/// state transition on the <see cref="Receipt"/> — fetched, rescheduled per the
/// retry scheme, or dead-lettered. It is provider/transport-agnostic and free of
/// Hangfire/Wolverine so the whole decision tree is unit-testable.
/// </summary>
public sealed class ReceiptFetchProcessor(
    IApplicationDbContext db,
    IUnitOfWork unitOfWork,
    IReceiptProvider provider,
    IReceiptRateLimiter rateLimiter,
    IReceiptDeadLetterQueue deadLetters,
    TimeProvider timeProvider,
    ILogger<ReceiptFetchProcessor> logger)
{
    /// <summary>Short back-off applied when the provider says we asked again too soon (code 4).</summary>
    private static readonly TimeSpan RetryTooSoonDelay = TimeSpan.FromMinutes(15);

    public async Task<ReceiptFetchProcessingResult> ProcessAsync(
        Guid receiptId, CancellationToken cancellationToken = default)
    {
        // Background work has no HTTP user, so the data-isolation filter would hide
        // every row — bypass it and load the receipt by its (unguessable) id.
        var receipt = await db.Receipts
            .IgnoreQueryFilters()
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == receiptId, cancellationToken);

        if (receipt is null)
        {
            logger.LogWarning("Receipt {ReceiptId} not found; nothing to fetch.", receiptId);
            return new(ReceiptFetchProcessingStatus.Skipped);
        }

        // Idempotency (T4.2.3): only a Pending receipt is actionable. A receipt that
        // is already Fetched/Failed/RetryLimit — e.g. a duplicate enqueue or a retry
        // racing the dispatcher — is a no-op.
        if (receipt.FetchStatus != ReceiptFetchStatus.Pending)
        {
            logger.LogDebug("Receipt {ReceiptId} is {Status}; skipping.", receiptId, receipt.FetchStatus);
            return new(ReceiptFetchProcessingStatus.Skipped);
        }

        if (string.IsNullOrWhiteSpace(receipt.QrRaw))
        {
            logger.LogWarning("Receipt {ReceiptId} has no QR payload; cannot fetch.", receiptId);
            return await DeadLetterAsync(receipt, "receipt has no QR payload", cancellationToken);
        }

        // Daily-quota guard. Fail-closed: an unreachable limiter pauses the queue
        // rather than risk exceeding the provider's hard limit (ARCHITECTURE.md §4).
        var decision = await rateLimiter.TryAcquireAsync(receipt.UserId, cancellationToken);
        switch (decision)
        {
            case RateLimitDecision.LimiterUnavailable:
                logger.LogWarning(
                    "Rate limiter unavailable; pausing receipt {ReceiptId} (fail-closed).", receiptId);
                return new(ReceiptFetchProcessingStatus.Paused);

            case RateLimitDecision.GlobalQuotaExceeded:
            case RateLimitDecision.UserQuotaExceeded:
                var until = StartOfNextUtcDay();
                var wait = until - timeProvider.GetUtcNow();
                receipt.ScheduleNextAttempt(until);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Quota {Decision} for user {UserId}; deferring receipt {ReceiptId} to {Until:o}.",
                    decision, receipt.UserId, receiptId, until);
                return new(ReceiptFetchProcessingStatus.Deferred, wait);
        }

        ReceiptFetchResult result;
        try
        {
            result = await provider.GetReceiptAsync(receipt.QrRaw, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The provider's resilience pipeline gave up (e.g. circuit open / timeout).
            // Count the attempt and reschedule, or dead-letter if the budget is spent.
            logger.LogError(ex, "Receipt {ReceiptId} provider call failed.", receiptId);
            receipt.RecordAttempt();
            return await RescheduleOrDeadLetterAsync(receipt, "provider call failed", cancellationToken);
        }

        // Code 4 — we asked again inside the provider's 10-minute window. Back off
        // briefly without spending an attempt against the retry budget.
        if (result.Outcome == ReceiptFetchOutcome.RetryTooSoon)
        {
            receipt.ScheduleNextAttempt(timeProvider.GetUtcNow() + RetryTooSoonDelay);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Receipt {ReceiptId} retry-too-soon; backing off {Delay}.",
                receiptId, RetryTooSoonDelay);
            return new(ReceiptFetchProcessingStatus.Rescheduled, RetryTooSoonDelay);
        }

        receipt.RecordAttempt();

        switch (result.Outcome)
        {
            case ReceiptFetchOutcome.Success when result.Data is not null:
                ReceiptMapper.Apply(receipt, result.Data, result.RawJson);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Receipt {ReceiptId} fetched on attempt {Attempt}.",
                    receiptId, receipt.FetchAttempts);
                return new(ReceiptFetchProcessingStatus.Completed);

            case ReceiptFetchOutcome.Invalid:
                receipt.MarkInvalid();
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return await DeadLetterAsync(receipt, "provider reported an invalid receipt (code 0)", cancellationToken);

            case ReceiptFetchOutcome.RetryLimitReached:
                receipt.MarkRetryLimitReached();
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return await DeadLetterAsync(receipt, "provider reported its retry limit (code 3)", cancellationToken);

            // NotYetAvailable (code 2), ProviderError (code 5), or an unexpected code.
            default:
                return await RescheduleOrDeadLetterAsync(
                    receipt, $"transient provider outcome ({result.Outcome})", cancellationToken);
        }
    }

    /// <summary>Schedules the next attempt per the retry scheme, or dead-letters when exhausted.</summary>
    private async Task<ReceiptFetchProcessingResult> RescheduleOrDeadLetterAsync(
        Receipt receipt, string reason, CancellationToken cancellationToken)
    {
        var delay = receipt.GetNextRetryDelay();
        if (delay is null)
        {
            receipt.MarkRetryLimitReached();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return await DeadLetterAsync(
                receipt, $"{reason}; retry budget exhausted after {receipt.FetchAttempts} attempts",
                cancellationToken);
        }

        receipt.ScheduleNextAttempt(timeProvider.GetUtcNow() + delay.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Receipt {ReceiptId} {Reason}; retry {Attempt}/{Max} scheduled in {Delay}.",
            receipt.Id, reason, receipt.FetchAttempts, Receipt.MaxFetchAttempts, delay.Value);
        return new(ReceiptFetchProcessingStatus.Rescheduled, delay.Value);
    }

    private async Task<ReceiptFetchProcessingResult> DeadLetterAsync(
        Receipt receipt, string reason, CancellationToken cancellationToken)
    {
        logger.LogWarning("Receipt {ReceiptId} dead-lettered: {Reason}.", receipt.Id, reason);
        await deadLetters.SendAsync(receipt.Id, reason, cancellationToken);
        return new(ReceiptFetchProcessingStatus.DeadLettered);
    }

    private DateTimeOffset StartOfNextUtcDay()
    {
        var now = timeProvider.GetUtcNow();
        return new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
    }
}
