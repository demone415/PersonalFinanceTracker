using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Receipts;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceTracker.UnitTests.Receipts;

/// <summary>
/// The background fetch decision tree (T4.2.3 / T4.2.4): idempotency, the
/// fail-closed rate-limiter, the retry scheme, terminal dead-lettering, and the
/// success path — all driven through the real <see cref="ReceiptFetchProcessor"/>
/// against an in-memory database with a faked provider, limiter and clock.
/// </summary>
public class ReceiptFetchProcessorTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly DateTimeOffset Now = new(2026, 6, 16, 10, 0, 0, TimeSpan.Zero);

    private sealed record StubCurrentUser(Guid? UserId, bool IsAdmin) : ICurrentUserService;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Harness
    {
        public AppDbContext Db { get; }
        public Mock<IReceiptProvider> Provider { get; } = new(MockBehavior.Strict);
        public Mock<IReceiptRateLimiter> RateLimiter { get; } = new(MockBehavior.Strict);
        public Mock<IReceiptDeadLetterQueue> DeadLetters { get; } = new();
        public ReceiptFetchProcessor Processor { get; }

        public Harness(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            Db = new AppDbContext(options, new StubCurrentUser(null, IsAdmin: true));

            Processor = new ReceiptFetchProcessor(
                Db,
                new UnitOfWork(Db),
                Provider.Object,
                RateLimiter.Object,
                DeadLetters.Object,
                new FixedTimeProvider(Now),
                NullLogger<ReceiptFetchProcessor>.Instance);
        }

        public Receipt SeedPending()
        {
            var receipt = Receipt.CreateForQrScan(
                UserId, 34_993, Now, "t=20200924T1837&s=349.93&fn=1&i=2&fp=3&n=1");
            Db.Receipts.Add(receipt);
            Db.SaveChanges();
            return receipt;
        }

        public void AllowQuota() =>
            RateLimiter.Setup(r => r.TryAcquireAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(RateLimitDecision.Allowed);

        public Receipt Reload(Guid id) =>
            Db.Receipts.IgnoreQueryFilters().AsNoTracking().Single(r => r.Id == id);
    }

    private static ReceiptFetchResult Success() =>
        ReceiptFetchResult.Successful(
            new ReceiptData("Org", "Addr", "7700000000", "Cashier", 1, "42",
                34_993, TaxationType.Usn, 100, "fn", "fp", []),
            "{\"json\":true}");

    [Fact]
    public async Task Success_MarksFetched_AndDoesNotDeadLetterOrReschedule()
    {
        var h = new Harness(nameof(Success_MarksFetched_AndDoesNotDeadLetterOrReschedule));
        var receipt = h.SeedPending();
        h.AllowQuota();
        h.Provider.Setup(p => p.GetReceiptAsync(receipt.QrRaw!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success());

        var result = await h.Processor.ProcessAsync(receipt.Id);

        Assert.Equal(ReceiptFetchProcessingStatus.Completed, result.Status);
        var saved = h.Reload(receipt.Id);
        Assert.Equal(ReceiptFetchStatus.Fetched, saved.FetchStatus);
        Assert.Equal("Org", saved.Organization);
        Assert.Equal(1, saved.FetchAttempts);
        h.DeadLetters.Verify(d => d.SendAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotYetAvailable_ReschedulesWithSixHourDelay_StaysPending()
    {
        var h = new Harness(nameof(NotYetAvailable_ReschedulesWithSixHourDelay_StaysPending));
        var receipt = h.SeedPending();
        h.AllowQuota();
        h.Provider.Setup(p => p.GetReceiptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReceiptFetchResult.Failed(ReceiptFetchOutcome.NotYetAvailable));

        var result = await h.Processor.ProcessAsync(receipt.Id);

        Assert.Equal(ReceiptFetchProcessingStatus.Rescheduled, result.Status);
        Assert.Equal(TimeSpan.FromHours(6), result.RetryDelay);
        var saved = h.Reload(receipt.Id);
        Assert.Equal(ReceiptFetchStatus.Pending, saved.FetchStatus);
        Assert.Equal(Now.AddHours(6), saved.NextFetchAt);
        Assert.Equal(1, saved.FetchAttempts);
    }

    [Fact]
    public async Task NotYetAvailable_RepeatedUntilBudgetExhausted_DeadLettersAsRetryLimit()
    {
        var h = new Harness(nameof(NotYetAvailable_RepeatedUntilBudgetExhausted_DeadLettersAsRetryLimit));
        var receipt = h.SeedPending();
        h.AllowQuota();
        h.Provider.Setup(p => p.GetReceiptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReceiptFetchResult.Failed(ReceiptFetchOutcome.NotYetAvailable));

        // Attempts 1–4 reschedule; the 5th exhausts the budget.
        var delays = new[] { TimeSpan.FromHours(6), TimeSpan.FromDays(1), TimeSpan.FromDays(3), TimeSpan.FromDays(10) };
        for (var i = 0; i < delays.Length; i++)
        {
            var r = await h.Processor.ProcessAsync(receipt.Id);
            Assert.Equal(ReceiptFetchProcessingStatus.Rescheduled, r.Status);
            Assert.Equal(delays[i], r.RetryDelay);
        }

        var final = await h.Processor.ProcessAsync(receipt.Id);

        Assert.Equal(ReceiptFetchProcessingStatus.DeadLettered, final.Status);
        var saved = h.Reload(receipt.Id);
        Assert.Equal(ReceiptFetchStatus.RetryLimit, saved.FetchStatus);
        Assert.Equal(Receipt.MaxFetchAttempts, saved.FetchAttempts);
        h.DeadLetters.Verify(d => d.SendAsync(receipt.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProviderRetryLimit_Code3_IsTerminalAndDeadLettered()
    {
        var h = new Harness(nameof(ProviderRetryLimit_Code3_IsTerminalAndDeadLettered));
        var receipt = h.SeedPending();
        h.AllowQuota();
        h.Provider.Setup(p => p.GetReceiptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReceiptFetchResult.Failed(ReceiptFetchOutcome.RetryLimitReached));

        var result = await h.Processor.ProcessAsync(receipt.Id);

        Assert.Equal(ReceiptFetchProcessingStatus.DeadLettered, result.Status);
        Assert.Equal(ReceiptFetchStatus.RetryLimit, h.Reload(receipt.Id).FetchStatus);
        h.DeadLetters.Verify(d => d.SendAsync(receipt.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidReceipt_Code0_IsTerminalFailedAndDeadLettered()
    {
        var h = new Harness(nameof(InvalidReceipt_Code0_IsTerminalFailedAndDeadLettered));
        var receipt = h.SeedPending();
        h.AllowQuota();
        h.Provider.Setup(p => p.GetReceiptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReceiptFetchResult.Failed(ReceiptFetchOutcome.Invalid));

        var result = await h.Processor.ProcessAsync(receipt.Id);

        Assert.Equal(ReceiptFetchProcessingStatus.DeadLettered, result.Status);
        Assert.Equal(ReceiptFetchStatus.Failed, h.Reload(receipt.Id).FetchStatus);
        h.DeadLetters.Verify(d => d.SendAsync(receipt.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetryTooSoon_Code4_BacksOffWithoutSpendingAnAttempt()
    {
        var h = new Harness(nameof(RetryTooSoon_Code4_BacksOffWithoutSpendingAnAttempt));
        var receipt = h.SeedPending();
        h.AllowQuota();
        h.Provider.Setup(p => p.GetReceiptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReceiptFetchResult.Failed(ReceiptFetchOutcome.RetryTooSoon));

        var result = await h.Processor.ProcessAsync(receipt.Id);

        Assert.Equal(ReceiptFetchProcessingStatus.Rescheduled, result.Status);
        Assert.Equal(TimeSpan.FromMinutes(15), result.RetryDelay);
        var saved = h.Reload(receipt.Id);
        Assert.Equal(ReceiptFetchStatus.Pending, saved.FetchStatus);
        Assert.Equal(0, saved.FetchAttempts); // budget untouched
    }

    [Fact]
    public async Task LimiterUnavailable_FailsClosed_DoesNotCallProvider()
    {
        var h = new Harness(nameof(LimiterUnavailable_FailsClosed_DoesNotCallProvider));
        var receipt = h.SeedPending();
        h.RateLimiter.Setup(r => r.TryAcquireAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateLimitDecision.LimiterUnavailable);

        var result = await h.Processor.ProcessAsync(receipt.Id);

        Assert.Equal(ReceiptFetchProcessingStatus.Paused, result.Status);
        var saved = h.Reload(receipt.Id);
        Assert.Equal(ReceiptFetchStatus.Pending, saved.FetchStatus);
        Assert.Equal(0, saved.FetchAttempts);
        h.Provider.Verify(p => p.GetReceiptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GlobalQuotaExceeded_DefersToNextUtcDay_DoesNotCallProvider()
    {
        var h = new Harness(nameof(GlobalQuotaExceeded_DefersToNextUtcDay_DoesNotCallProvider));
        var receipt = h.SeedPending();
        h.RateLimiter.Setup(r => r.TryAcquireAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateLimitDecision.GlobalQuotaExceeded);

        var result = await h.Processor.ProcessAsync(receipt.Id);

        Assert.Equal(ReceiptFetchProcessingStatus.Deferred, result.Status);
        var saved = h.Reload(receipt.Id);
        Assert.Equal(ReceiptFetchStatus.Pending, saved.FetchStatus);
        Assert.Equal(new DateTimeOffset(2026, 6, 17, 0, 0, 0, TimeSpan.Zero), saved.NextFetchAt);
        Assert.Equal(0, saved.FetchAttempts);
        h.Provider.Verify(p => p.GetReceiptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AlreadyFetched_IsIdempotentNoOp()
    {
        var h = new Harness(nameof(AlreadyFetched_IsIdempotentNoOp));
        var receipt = h.SeedPending();
        // Drive it to Fetched first.
        h.AllowQuota();
        h.Provider.Setup(p => p.GetReceiptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success());
        await h.Processor.ProcessAsync(receipt.Id);
        h.Provider.Invocations.Clear();
        h.RateLimiter.Invocations.Clear();

        var result = await h.Processor.ProcessAsync(receipt.Id);

        Assert.Equal(ReceiptFetchProcessingStatus.Skipped, result.Status);
        h.Provider.Verify(p => p.GetReceiptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        h.RateLimiter.Verify(r => r.TryAcquireAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MissingReceipt_IsSkipped()
    {
        var h = new Harness(nameof(MissingReceipt_IsSkipped));

        var result = await h.Processor.ProcessAsync(Guid.NewGuid());

        Assert.Equal(ReceiptFetchProcessingStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task ProviderThrows_CountsAttemptAndReschedules()
    {
        var h = new Harness(nameof(ProviderThrows_CountsAttemptAndReschedules));
        var receipt = h.SeedPending();
        h.AllowQuota();
        h.Provider.Setup(p => p.GetReceiptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("circuit open"));

        var result = await h.Processor.ProcessAsync(receipt.Id);

        Assert.Equal(ReceiptFetchProcessingStatus.Rescheduled, result.Status);
        Assert.Equal(TimeSpan.FromHours(6), result.RetryDelay);
        var saved = h.Reload(receipt.Id);
        Assert.Equal(ReceiptFetchStatus.Pending, saved.FetchStatus);
        Assert.Equal(1, saved.FetchAttempts);
    }
}
