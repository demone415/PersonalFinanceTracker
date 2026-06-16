using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.UnitTests.Receipts;

/// <summary>
/// The domain retry scheme for a "not yet in the tax DB" receipt (T4.2.3): the
/// 2nd…5th attempt wait 6h / 1d / 3d / 10d, and the 5-attempt budget is then
/// exhausted. Also covers the explicit terminal/transient state transitions.
/// </summary>
public class ReceiptRetryScheduleTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static Receipt NewPending() =>
        Receipt.CreateForQrScan(UserId, 10_000, DateTimeOffset.UnixEpoch, "t=1&s=1&fn=1&i=1&fp=1&n=1");

    [Fact]
    public void GetNextRetryDelay_FollowsTheScheme_ThenExhausts()
    {
        var receipt = NewPending();
        var expected = new[]
        {
            TimeSpan.FromHours(6),
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(3),
            TimeSpan.FromDays(10),
        };

        for (var attempt = 0; attempt < expected.Length; attempt++)
        {
            receipt.RecordAttempt(); // attempt N just made
            Assert.True(receipt.HasAttemptsRemaining);
            Assert.Equal(expected[attempt], receipt.GetNextRetryDelay());
        }

        // The 5th attempt spends the budget — no further delay is offered.
        receipt.RecordAttempt();
        Assert.False(receipt.HasAttemptsRemaining);
        Assert.Null(receipt.GetNextRetryDelay());
        Assert.Equal(Receipt.MaxFetchAttempts, receipt.FetchAttempts);
    }

    [Fact]
    public void ScheduleNextAttempt_KeepsPendingAndRecordsDueTime()
    {
        var receipt = NewPending();
        var due = new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero);

        receipt.ScheduleNextAttempt(due);

        Assert.Equal(ReceiptFetchStatus.Pending, receipt.FetchStatus);
        Assert.Equal(due, receipt.NextFetchAt);
    }

    [Fact]
    public void MarkInvalid_IsTerminalFailed()
    {
        var receipt = NewPending();
        receipt.MarkInvalid();
        Assert.Equal(ReceiptFetchStatus.Failed, receipt.FetchStatus);
    }

    [Fact]
    public void MarkRetryLimitReached_IsTerminalRetryLimit()
    {
        var receipt = NewPending();
        receipt.MarkRetryLimitReached();
        Assert.Equal(ReceiptFetchStatus.RetryLimit, receipt.FetchStatus);
    }

    [Fact]
    public void CreateForQrScan_RequiresQrPayload()
    {
        Assert.Throws<ArgumentException>(() =>
            Receipt.CreateForQrScan(UserId, 1, DateTimeOffset.UnixEpoch, qrRaw: "  "));
    }
}
