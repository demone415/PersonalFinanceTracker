using FinanceTracker.Application.Common.Exceptions;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Application.Features.Receipts;

/// <summary>
/// Producer side of the receipt pipeline (T4.3.2 backend / T4.2.1): accepts a
/// scanned QR, creates the accrual + a Pending receipt, and publishes
/// <see cref="ReceiptFetchRequested"/> so the background worker fetches it. Also
/// reports fetch progress for the polling status endpoint (T4.3.3).
/// </summary>
public sealed class ReceiptScanService(
    IApplicationDbContext db,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMessagePublisher publisher,
    IReceiptFeatureGate featureGate,
    ILogger<ReceiptScanService> logger)
{
    /// <summary>Stable error code returned when scanning is off (no provider token).</summary>
    public const string ScanningDisabledCode = "receipt_scanning_disabled";

    public async Task<ScanQrResponse> ScanAsync(ScanQrRequest request, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("Authentication required.");

        if (!featureGate.IsScanningEnabled)
        {
            throw new FeatureDisabledException(
                ScanningDisabledCode,
                "Receipt scanning is disabled: the ProverkaCheka provider token is not configured.");
        }

        // Validated by ScanQrRequestValidator before we get here; re-parse to build
        // the entities (and guard defensively).
        if (!QrCodeParser.TryParse(request.QrRaw, out var qr) || qr is null)
        {
            throw new FormatException("Invalid receipt QR string.");
        }

        var receipt = Receipt.CreateForQrScan(
            userId, qr.SumInKopecks, qr.Timestamp, qr.Raw,
            fd: qr.FiscalDocumentNumber, fn: qr.FiscalDriveNumber, fpd: qr.FiscalSign);

        var accrual = new Accrual(
            userId,
            qr.Sum,
            qr.Timestamp,
            qr.AccrualType ?? AccrualType.Expense,
            description: "Чек (ожидает загрузки)");
        accrual.SetReceipt(receipt.Id);

        db.Receipts.Add(receipt);
        db.Accruals.Add(accrual);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Publish after the commit: the receipt row is the source of truth, so even
        // if this message is lost the dispatcher still picks the receipt up.
        await publisher.PublishAsync(
            new ReceiptFetchRequested(receipt.Id, userId, qr.Raw), cancellationToken);

        logger.LogInformation(
            "Accepted QR scan: accrual {AccrualId}, receipt {ReceiptId} queued for fetch.",
            accrual.Id, receipt.Id);

        return new ScanQrResponse(accrual.Id, receipt.Id, receipt.FetchStatus.ToString());
    }

    public async Task<ReceiptStatusResponse> GetStatusAsync(
        Guid accrualId, CancellationToken cancellationToken = default)
    {
        var accrual = await db.Accruals
            .FirstOrDefaultAsync(a => a.Id == accrualId, cancellationToken)
            ?? throw new NotFoundException(nameof(Accrual), accrualId);

        if (!currentUser.IsAdmin && accrual.UserId != currentUser.UserId)
        {
            throw new ForbiddenAccessException("You do not own this accrual.");
        }

        if (!accrual.ReceiptId.HasValue)
        {
            throw new NotFoundException(nameof(Receipt), accrualId);
        }

        var receipt = await db.Receipts
            .FirstOrDefaultAsync(r => r.Id == accrual.ReceiptId.Value, cancellationToken)
            ?? throw new NotFoundException(nameof(Receipt), accrual.ReceiptId.Value);

        int? position = receipt.FetchStatus == ReceiptFetchStatus.Pending
            ? await ComputeQueuePositionAsync(receipt, cancellationToken)
            : null;

        return new ReceiptStatusResponse(
            accrualId, receipt.Id, receipt.FetchStatus.ToString(), receipt.FetchAttempts, position);
    }

    /// <summary>
    /// Approximate 1-based position in the global FIFO queue: how many other
    /// Pending receipts are due ahead of this one. Counted across all users
    /// (<see cref="EntityFrameworkQueryableExtensions.IgnoreQueryFilters"/>) since
    /// the queue is global; only the count is exposed, never others' data. Receipts
    /// with the same due-time share a rank, which is fine for a progress hint.
    /// </summary>
    private async Task<int> ComputeQueuePositionAsync(Receipt receipt, CancellationToken cancellationToken)
    {
        // A null NextFetchAt (brand-new scan) sorts before any scheduled retry.
        var due = receipt.NextFetchAt ?? DateTimeOffset.MinValue;

        var ahead = await db.Receipts
            .IgnoreQueryFilters()
            .Where(r => r.FetchStatus == ReceiptFetchStatus.Pending && r.Id != receipt.Id)
            .Where(r => (r.NextFetchAt ?? DateTimeOffset.MinValue) < due)
            .CountAsync(cancellationToken);

        return ahead + 1;
    }
}
