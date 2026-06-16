namespace FinanceTracker.Application.Common.Exceptions;

/// <summary>
/// Thrown when a feature is switched off by configuration rather than by a
/// transient fault — e.g. receipt scanning when the provider token is missing.
/// Surfaced to clients as <c>503</c> with a machine-readable <see cref="Code"/>
/// so the UI can disable the related controls instead of retrying.
/// </summary>
public sealed class FeatureDisabledException(string code, string message) : Exception(message)
{
    /// <summary>Stable identifier for the disabled feature, e.g. <c>receipt_scanning_disabled</c>.</summary>
    public string Code { get; } = code;
}
