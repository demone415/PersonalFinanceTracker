namespace FinanceTracker.Application.Features.Receipts;

/// <summary>Body of <c>POST /api/v1/accruals/scan-qr</c>: the raw receipt QR string.</summary>
public sealed record ScanQrRequest(string QrRaw);

/// <summary>Result of accepting a QR scan: the created accrual/receipt and initial status.</summary>
public sealed record ScanQrResponse(Guid AccrualId, Guid ReceiptId, string FetchStatus);

/// <summary>
/// Progress of an async receipt fetch (<c>GET /api/v1/accruals/{id}/receipt-status</c>):
/// the status, attempts so far, and — while still queued — the 1-based position in
/// the global FIFO queue.
/// </summary>
public sealed record ReceiptStatusResponse(
    Guid AccrualId,
    Guid? ReceiptId,
    string FetchStatus,
    int FetchAttempts,
    int? QueuePosition);
