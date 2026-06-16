using FinanceTracker.Application.Features.Receipts;

namespace FinanceTracker.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the external fiscal-receipt service (ПроверкаЧека). The
/// concrete implementation is a Refit client wrapped in a Polly resilience
/// pipeline (ARCHITECTURE.md §4); callers depend only on this interface so the
/// provider can be swapped or faked in tests.
/// </summary>
public interface IReceiptProvider
{
    /// <summary>
    /// Requests the composition of a receipt from its raw QR string (method 2:
    /// <c>qrraw</c>). Returns a classified <see cref="ReceiptFetchResult"/>;
    /// transport/HTTP failures surface as exceptions handled by the resilience
    /// pipeline, while business outcomes (codes 0–5) are returned, not thrown.
    /// </summary>
    Task<ReceiptFetchResult> GetReceiptAsync(string qrRaw, CancellationToken ct = default);
}
