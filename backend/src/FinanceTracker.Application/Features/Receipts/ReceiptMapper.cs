using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Features.Receipts;

/// <summary>
/// Manual mapping of a parsed provider <see cref="ReceiptData"/> onto a domain
/// <see cref="Receipt"/> (T4.1.4 — no mapping libraries). Builds the
/// <see cref="ReceiptItem"/> graph and delegates the state change to the rich
/// domain method so invariants stay on the entity.
/// </summary>
public static class ReceiptMapper
{
    /// <summary>Applies <paramref name="data"/> to <paramref name="receipt"/> and marks it fetched.</summary>
    public static void Apply(Receipt receipt, ReceiptData data, string? rawJson)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(data);

        var items = data.Items
            .Select(i => new ReceiptItem(receipt.Id, i.Name, i.Price, i.Quantity, i.Sum))
            .ToList();

        receipt.ApplyFetchedData(
            organization: data.Organization,
            address: data.Address,
            inn: data.Inn,
            cashier: data.Cashier,
            shiftNumber: data.ShiftNumber,
            externalNumber: data.ExternalNumber,
            totalSumInKopecks: data.TotalSumInKopecks,
            taxationType: data.TaxationType,
            fd: data.FiscalDocumentNumber,
            fn: data.FiscalDriveNumber,
            fpd: data.FiscalSign,
            items: items,
            rawMetadata: rawJson);
    }
}
