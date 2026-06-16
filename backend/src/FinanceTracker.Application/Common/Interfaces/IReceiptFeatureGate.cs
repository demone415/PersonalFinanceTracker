namespace FinanceTracker.Application.Common.Interfaces;

/// <summary>
/// Whether the receipt-fetching feature is operable. It depends on the
/// ProverkaCheka provider token being configured: with no token the provider
/// cannot be called, so QR scanning is switched off end to end — new scans are
/// rejected, already-queued receipts pause, and the UI disables the controls.
/// </summary>
public interface IReceiptFeatureGate
{
    /// <summary><c>true</c> when the provider token is configured and scanning may run.</summary>
    bool IsScanningEnabled { get; }
}
