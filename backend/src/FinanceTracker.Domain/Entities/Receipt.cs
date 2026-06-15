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

    private readonly List<ReceiptItem> _items = [];
    public IReadOnlyCollection<ReceiptItem> Items => _items;

    private Receipt() { }

    /// <summary>Creates a stub receipt for QR-scan flow before fetch completes.</summary>
    public Receipt(Guid userId, long amountInKopecks, DateTimeOffset date,
        long? fd = null, string? fn = null, string? fpd = null)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        AmountInKopecks = amountInKopecks;
        Date = date;
        FD = fd;
        FN = fn;
        FPD = fpd;
        FetchStatus = ReceiptFetchStatus.Pending;
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

    public void MarkFetched(string? rawMetadata)
    {
        FetchStatus = ReceiptFetchStatus.Fetched;
        RawMetadata = rawMetadata;
        FetchAttempts++;
    }

    public void MarkFailed()
    {
        FetchAttempts++;
        FetchStatus = FetchAttempts >= 5 ? ReceiptFetchStatus.RetryLimit : ReceiptFetchStatus.Failed;
    }

    public void ScheduleNextAttempt(DateTimeOffset nextFetchAt)
    {
        FetchAttempts++;
        NextFetchAt = nextFetchAt;
    }
}
