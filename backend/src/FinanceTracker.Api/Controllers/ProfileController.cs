using FinanceTracker.Application.Features.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

/// <summary>
/// The caller's application profile (Epic 8, T8.1.2): base-currency selection and
/// display name. The profile is identified by the authenticated GoTrue user, so
/// no id is taken from the route. All endpoints require authentication.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/profile")]
public sealed class ProfileController : ControllerBase
{
    /// <summary>Returns the caller's profile, creating a default one on first access.</summary>
    [HttpGet]
    public Task<ProfileDto> Get(
        [FromServices] ProfileService profiles,
        CancellationToken cancellationToken) =>
        profiles.GetCurrentAsync(cancellationToken);

    /// <summary>Updates the caller's display name and base currency.</summary>
    [HttpPut]
    public Task<ProfileDto> Update(
        [FromBody] UpdateProfileRequest request,
        [FromServices] ProfileService profiles,
        CancellationToken cancellationToken) =>
        profiles.UpdateAsync(request, cancellationToken);
}
