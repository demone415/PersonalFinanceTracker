using System.Text.Json;
using FinanceTracker.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace FinanceTracker.Infrastructure.Identity;

/// <summary>
/// Reads the authenticated caller from the validated GoTrue JWT on the current
/// <see cref="HttpContext"/> (T1.2.3, ARCHITECTURE.md §11.1): <c>UserId</c> from
/// the <c>sub</c> claim and <c>IsAdmin</c> strictly from <c>app_metadata.role</c>.
/// </summary>
/// <remarks>
/// The role is taken only from <c>app_metadata</c> (server-controlled). It is
/// never read from <c>user_metadata</c>, which the user can edit — that would be
/// a privilege-escalation path to admin.
/// </remarks>
internal sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid? UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value, out var id)
            ? id
            : null;

    public bool IsAdmin
    {
        get
        {
            var appMetadata = httpContextAccessor.HttpContext?.User.FindFirst("app_metadata")?.Value;
            if (string.IsNullOrEmpty(appMetadata))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(appMetadata);
                return document.RootElement.TryGetProperty("role", out var role)
                    && role.ValueKind == JsonValueKind.String
                    && string.Equals(role.GetString(), "admin", StringComparison.Ordinal);
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
