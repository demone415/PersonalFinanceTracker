using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Domain.Entities;

public class Accrual : IUserOwnedEntity
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset Date { get; private set; }
    public AccrualType Type { get; private set; }
    public string Currency { get; private set; }
    public decimal? ExchangeRate { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string? Description { get; private set; }
    public bool IncludeInStats { get; private set; }
    public Guid? GroupId { get; private set; }
    public Guid? ReceiptId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Navigation
    public Category? Category { get; private set; }
    public Receipt? Receipt { get; private set; }

    /// <summary>
    /// Amount expressed in the user's base currency (Epic 8, T8.1.4). The
    /// <see cref="ExchangeRate"/> captured at transaction time is the multiplier
    /// from <see cref="Currency"/> to the base currency; a <c>null</c> rate means
    /// the transaction is already in the base currency (1:1). This is the
    /// canonical conversion rule every aggregate uses — see the "Currency
    /// aggregation contract" in CLAUDE.md. (Not mapped; ignored in EF config.)
    /// </summary>
    public decimal AmountInBaseCurrency => ExchangeRate.HasValue ? Amount * ExchangeRate.Value : Amount;

    private readonly List<AccrualTag> _tags = [];
    public IReadOnlyCollection<AccrualTag> Tags => _tags;

    private Accrual() { Currency = "RUB"; }

    public Accrual(
        Guid userId,
        decimal amount,
        DateTimeOffset date,
        AccrualType type,
        string currency = "RUB",
        Guid? categoryId = null,
        string? description = null,
        bool includeInStats = true,
        Guid? groupId = null,
        decimal? exchangeRate = null)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        Amount = amount;
        Date = date;
        Type = type;
        Currency = currency;
        CategoryId = categoryId;
        Description = description;
        IncludeInStats = includeInStats;
        GroupId = groupId;
        ExchangeRate = exchangeRate;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void Update(
        decimal amount,
        DateTimeOffset date,
        AccrualType type,
        string currency,
        Guid? categoryId,
        string? description,
        bool includeInStats,
        Guid? groupId,
        decimal? exchangeRate)
    {
        Amount = amount;
        Date = date;
        Type = type;
        Currency = currency;
        CategoryId = categoryId;
        Description = description;
        IncludeInStats = includeInStats;
        GroupId = groupId;
        ExchangeRate = exchangeRate;
    }

    public void SetReceipt(Guid receiptId) => ReceiptId = receiptId;

    public void SetTags(IEnumerable<string> tags)
    {
        _tags.Clear();
        foreach (var tag in tags.Distinct(StringComparer.OrdinalIgnoreCase))
            _tags.Add(new AccrualTag(Id, tag));
    }
}
