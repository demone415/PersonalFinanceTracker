using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Features.Receipts;

/// <summary>
/// The provider-agnostic outcome of a single fetch attempt, mapped from the
/// ПроверкаЧека <c>code</c> field (ARCHITECTURE.md §4):
/// 0 → <see cref="Invalid"/>, 1 → <see cref="Success"/>, 2 → <see cref="NotYetAvailable"/>,
/// 3 → <see cref="RetryLimitReached"/>, 4 → <see cref="RetryTooSoon"/>,
/// 5 → <see cref="ProviderError"/>.
/// </summary>
public enum ReceiptFetchOutcome
{
    /// <summary>code 1 — receipt found; <see cref="ReceiptFetchResult.Data"/> is populated.</summary>
    Success,

    /// <summary>code 2 — receipt not yet in the tax DB; reschedule per the retry scheme.</summary>
    NotYetAvailable,

    /// <summary>code 3 — provider reports the per-receipt retry limit is exhausted → terminal.</summary>
    RetryLimitReached,

    /// <summary>code 4 — last attempt was too recent (&lt; 10 min); retry later.</summary>
    RetryTooSoon,

    /// <summary>code 0 — the receipt is invalid → terminal.</summary>
    Invalid,

    /// <summary>code 5 (or an unexpected code) — provider-side error; transient, retry.</summary>
    ProviderError,
}

/// <summary>
/// Result of <see cref="Common.Interfaces.IReceiptProvider.GetReceiptAsync"/>:
/// the classified <see cref="Outcome"/>, the parsed receipt when successful, and
/// the raw provider JSON kept for <c>Receipt.RawMetadata</c> / auditing.
/// </summary>
public sealed record ReceiptFetchResult(
    ReceiptFetchOutcome Outcome,
    ReceiptData? Data,
    string? RawJson)
{
    public bool IsSuccess => Outcome == ReceiptFetchOutcome.Success && Data is not null;

    /// <summary>Terminal outcomes never produce a usable receipt no matter how often retried.</summary>
    public bool IsTerminalFailure =>
        Outcome is ReceiptFetchOutcome.Invalid or ReceiptFetchOutcome.RetryLimitReached;

    public static ReceiptFetchResult Successful(ReceiptData data, string? rawJson) =>
        new(ReceiptFetchOutcome.Success, data, rawJson);

    public static ReceiptFetchResult Failed(ReceiptFetchOutcome outcome, string? rawJson = null) =>
        new(outcome, null, rawJson);
}

/// <summary>
/// The receipt payload parsed from the provider's <c>data.json</c>, already
/// normalised to domain types and units. Monetary totals are in kopecks (as the
/// provider returns them); per-item price/sum are in rubles for storage on
/// <see cref="Domain.Entities.ReceiptItem"/>.
/// </summary>
public sealed record ReceiptData(
    string? Organization,
    string? Address,
    string? Inn,
    string? Cashier,
    int? ShiftNumber,
    string? ExternalNumber,
    long TotalSumInKopecks,
    TaxationType? TaxationType,
    long? FiscalDocumentNumber,
    string? FiscalDriveNumber,
    string? FiscalSign,
    IReadOnlyList<ReceiptItemData> Items);

/// <summary>A single position from the receipt's <c>data.json.items</c> array, in rubles.</summary>
public sealed record ReceiptItemData(
    string Name,
    decimal Price,
    decimal Quantity,
    decimal Sum);
