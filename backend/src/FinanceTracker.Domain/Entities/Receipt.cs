using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Domain.Entities;

public class Receipt : IUserOwnedEntity
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    // Fiscal fields from QR / ПроверкаЧека
    public long? FD { get; private set; }
    public string? FN { get; private set; }
    public string? FPD { get; private set; }
    public long AmountInKopecks { get; private set; }
    public DateTimeOffset Date { get; private set; }
    public string? ExternalNumber { get; private set; }
    public int? ShiftNumber { get; private set; }
    public string? INN { get; private set; }
    public string? Cashier { get; private set; }
    public string? Organization { get; private set; }
    public string? Address { get; private set; }
    public TaxationType? TaxationType { get; private set; }

    // Fetch tracking
    public ReceiptFetchStatus FetchStatus { get; private set; }
    public int FetchAttempts { get; private set; }
    public DateTimeOffset? NextFetchAt { get; private set; }
    public string? RawMetadata { get; private set; }

    /// <summary>The raw QR payload (<c>qrraw</c>) the provider is asked to resolve;
    /// persisted so a rescheduled retry days later can replay the same request.</summary>
    public string? QrRaw { get; private set; }

    /// <summary>Maximum number of provider attempts before giving up (ARCHITECTURE.md §4).</summary>
    public const int MaxFetchAttempts = 5;

    /// <summary>
    /// Reschedule scheme for a "not yet in the tax DB" outcome (code 2): the wait
    /// before the 2nd…5th attempt. With 4 delays the receipt is tried at most
    /// <see cref="MaxFetchAttempts"/> times.
    /// </summary>
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromHours(6),
        TimeSpan.FromDays(1),
        TimeSpan.FromDays(3),
        TimeSpan.FromDays(10),
    ];

    private readonly List<ReceiptItem> _items = [];
    public IReadOnlyCollection<ReceiptItem> Items => _items;

    private Receipt() { }

    /// <summary>Creates a stub receipt for the QR-scan flow, before the fetch runs.</summary>
    public static Receipt CreateForQrScan(
        Guid userId, long amountInKopecks, DateTimeOffset date, string qrRaw,
        long? fd = null, string? fn = null, string? fpd = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(qrRaw);
        return new Receipt
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            AmountInKopecks = amountInKopecks,
            Date = date,
            QrRaw = qrRaw,
            FD = fd,
            FN = fn,
            FPD = fpd,
            FetchStatus = ReceiptFetchStatus.Pending,
        };
    }

    /// <summary>Creates a manually-entered receipt (no QR fetch needed).</summary>
    public static Receipt CreateManual(Guid userId, long amountInKopecks, DateTimeOffset date,
        string? organization = null, string? address = null, string? inn = null)
    {
        var r = new Receipt
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            AmountInKopecks = amountInKopecks,
            Date = date,
            Organization = organization,
            Address = address,
            INN = inn,
            FetchStatus = ReceiptFetchStatus.Fetched,
        };
        return r;
    }

    public void AddItem(ReceiptItem item) => _items.Add(item);

    public void RemoveItem(ReceiptItem item) => _items.Remove(item);

    /// <summary>Call once per provider invocation before applying the outcome.</summary>
    public void RecordAttempt() => FetchAttempts++;

    /// <summary>True while another provider attempt is still permitted.</summary>
    public bool HasAttemptsRemaining => FetchAttempts < MaxFetchAttempts;

    /// <summary>
    /// The wait before the next attempt given how many have already been made, or
    /// <c>null</c> when the retry budget is exhausted (caller moves to RetryLimit).
    /// </summary>
    public TimeSpan? GetNextRetryDelay()
    {
        var index = FetchAttempts - 1; // delay that follows the attempt just made
        return index >= 0 && index < RetryDelays.Length ? RetryDelays[index] : null;
    }

    public void MarkFetched(string? rawMetadata)
    {
        FetchStatus = ReceiptFetchStatus.Fetched;
        RawMetadata = rawMetadata;
    }

    /// <summary>
    /// Applies the fiscal details fetched from the provider, replaces the item
    /// list, and marks the receipt as fetched. The QR-derived <see cref="Date"/>
    /// is authoritative and left untouched; the provider total overrides the
    /// QR amount only when it is present (&gt; 0).
    /// </summary>
    public void ApplyFetchedData(
        string? organization,
        string? address,
        string? inn,
        string? cashier,
        int? shiftNumber,
        string? externalNumber,
        long totalSumInKopecks,
        TaxationType? taxationType,
        long? fd,
        string? fn,
        string? fpd,
        IEnumerable<ReceiptItem> items,
        string? rawMetadata)
    {
        Organization = organization;
        Address = address;
        INN = inn;
        Cashier = cashier;
        ShiftNumber = shiftNumber;
        ExternalNumber = externalNumber;
        if (totalSumInKopecks > 0)
        {
            AmountInKopecks = totalSumInKopecks;
        }

        TaxationType = taxationType;
        FD = fd ?? FD;
        FN = fn ?? FN;
        FPD = fpd ?? FPD;

        _items.Clear();
        _items.AddRange(items);

        FetchStatus = ReceiptFetchStatus.Fetched;
        RawMetadata = rawMetadata;
    }

    /// <summary>Terminal failure for a permanently unusable receipt (provider code 0).</summary>
    public void MarkInvalid() => FetchStatus = ReceiptFetchStatus.Failed;

    /// <summary>
    /// Terminal failure after the retry budget is spent or the provider reports its
    /// own per-receipt retry limit (code 3) — the caller routes this to the DLQ.
    /// </summary>
    public void MarkRetryLimitReached() => FetchStatus = ReceiptFetchStatus.RetryLimit;

    /// <summary>Keeps the receipt Pending and records when the next attempt is due.</summary>
    public void ScheduleNextAttempt(DateTimeOffset nextFetchAt)
    {
        FetchStatus = ReceiptFetchStatus.Pending;
        NextFetchAt = nextFetchAt;
    }
}
