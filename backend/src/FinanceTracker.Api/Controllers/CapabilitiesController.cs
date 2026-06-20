using FinanceTracker.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

/// <summary>
/// Reports which optional features the backend can serve, so the SPA can disable
/// the related controls up front instead of only reacting to errors.
/// <para>
/// Flags are evaluated per request from the live configuration, because the
/// provider token can be added (or removed) on the backend independently of any
/// frontend deploy. The SPA must therefore <b>query this at runtime</b> and react
/// to changes — never hard-code a feature as on/off, and don't cache it for the
/// whole session. <see cref="CapabilitiesResponse.ReceiptScanning"/> depends on
/// the ProverkaCheka token (see <see cref="IReceiptFeatureGate"/>);
/// <see cref="CapabilitiesResponse.FnsImport"/> does not and is always on.
/// </para>
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/capabilities")]
public sealed class CapabilitiesController : ControllerBase
{
    [HttpGet]
    public CapabilitiesResponse Get([FromServices] IReceiptFeatureGate featureGate) =>
        new(
            // QR scanning calls the provider, so it needs the token.
            ReceiptScanning: featureGate.IsScanningEnabled,
            // FNS import parses a user-supplied file and never calls the provider,
            // so it works with or without the token — always available.
            FnsImport: true);
}

/// <summary>Feature flags consumed by the frontend to enable/disable UI controls.</summary>
/// <param name="ReceiptScanning">
/// QR scanning + background receipt fetching. <c>false</c> when the ProverkaCheka
/// token is not configured — the only condition that disables it.
/// </param>
/// <param name="FnsImport">
/// Import from the «Налоги ФЛ» Excel export. Always <c>true</c>: the import parses
/// a user-supplied <c>.xlsx</c> file and does <b>not</b> call the ProverkaCheka
/// provider, so it stays available even when no provider token is configured.
/// </param>
public sealed record CapabilitiesResponse(bool ReceiptScanning, bool FnsImport);
