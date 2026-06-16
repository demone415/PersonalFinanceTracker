using FinanceTracker.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

/// <summary>
/// Reports which optional features the backend can serve, so the SPA can disable
/// the related controls up front instead of only reacting to errors.
/// <para>
/// The flag is evaluated per request from the live configuration, because the
/// provider token can be added (or removed) on the backend independently of any
/// frontend deploy. The SPA must therefore <b>query this at runtime</b> and react
/// to changes — never hard-code the feature as on/off, and don't cache it for the
/// whole session. Capabilities depend on the ProverkaCheka token
/// (see <see cref="IReceiptFeatureGate"/>).
/// </para>
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
/// <param name="ReceiptScanning">
/// QR scanning + background receipt fetching. <c>false</c> when the ProverkaCheka
/// token is not configured — the only condition that disables it.
/// </param>
/// <param name="FnsImport">
/// Import from the «Налоги ФЛ» export. This import does <b>not</b> technically need
/// the ProverkaCheka token (it parses a user-supplied JSON file), but it is gated
/// on the same flag as a deliberate product decision: when receipt features are
/// off we present the whole "receipts" area as unavailable. Decouple this from the
/// token gate if FNS import should ever be offered on its own.
/// </param>
public sealed record CapabilitiesResponse(bool ReceiptScanning, bool FnsImport);
