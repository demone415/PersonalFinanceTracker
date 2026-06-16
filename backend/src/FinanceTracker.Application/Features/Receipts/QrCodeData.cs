using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Features.Receipts;

/// <summary>
/// The fields encoded in a fiscal-receipt QR string, after validation
/// (ARCHITECTURE.md §4). Produced by <see cref="QrCodeParser"/> before a scan is
/// accepted and enqueued; the raw string is what we later hand to the provider.
/// </summary>
/// <param name="Timestamp">Receipt date/time (QR field <c>t</c>).</param>
/// <param name="Sum">Total in rubles (QR field <c>s</c>).</param>
/// <param name="FiscalDriveNumber">ФН — fiscal drive number (QR field <c>fn</c>).</param>
/// <param name="FiscalDocumentNumber">ФД — fiscal document number (QR field <c>i</c>).</param>
/// <param name="FiscalSign">ФП — fiscal sign (QR field <c>fp</c>).</param>
/// <param name="OperationType">Operation type 1–4 (QR field <c>n</c>).</param>
/// <param name="Raw">The original, normalised QR payload sent to the provider as <c>qrraw</c>.</param>
public sealed record QrCodeData(
    DateTimeOffset Timestamp,
    decimal Sum,
    string FiscalDriveNumber,
    long FiscalDocumentNumber,
    string FiscalSign,
    int OperationType,
    string Raw)
{
    /// <summary>Total in kopecks — how a <see cref="Domain.Entities.Receipt"/> stores the amount.</summary>
    public long SumInKopecks => (long)Math.Round(Sum * 100m, MidpointRounding.AwayFromZero);

    /// <summary>Maps the QR <c>n</c> field onto the domain operation type, when recognised.</summary>
    public AccrualType? AccrualType => OperationType switch
    {
        1 => Domain.Enums.AccrualType.Income,
        2 => Domain.Enums.AccrualType.ReturnIncome,
        3 => Domain.Enums.AccrualType.Expense,
        4 => Domain.Enums.AccrualType.ReturnExpense,
        _ => null,
    };
}
