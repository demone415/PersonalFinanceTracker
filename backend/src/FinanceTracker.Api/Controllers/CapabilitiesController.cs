using FinanceTracker.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

/// <summary>
/// Reports which optional features the backend can serve, so the SPA can disable
/// the related controls up front instead of only reacting to errors. Receipt
/// scanning and FNS import depend on the ProverkaCheka provider token being
/// configured (see <see cref="IReceiptFeatureGate"/>).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/capabilities")]
public sealed class CapabilitiesController : ControllerBase
{
    [HttpGet]
    public CapabilitiesResponse Get([FromServices] IReceiptFeatureGate featureGate)
    {
        var scanningEnabled = featureGate.IsScanningEnabled;
        return new CapabilitiesResponse(
            ReceiptScanning: scanningEnabled,
            FnsImport: scanningEnabled);
    }
}

/// <summary>Feature flags consumed by the frontend to enable/disable UI controls.</summary>
public sealed record CapabilitiesResponse(bool ReceiptScanning, bool FnsImport);
