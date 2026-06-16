using FinanceTracker.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.ExternalProviders.ProverkaChecka;

/// <summary>
/// Gates receipt scanning on whether the ProverkaCheka token is configured
/// (<see cref="ProverkaCheckaOptions.Token"/>). The token is a server-side secret
/// (§11.10); without it the provider rejects every call, so the feature is off.
/// </summary>
internal sealed class ProverkaCheckaReceiptFeatureGate(IOptions<ProverkaCheckaOptions> options)
    : IReceiptFeatureGate
{
    public bool IsScanningEnabled => !string.IsNullOrWhiteSpace(options.Value.Token);
}
