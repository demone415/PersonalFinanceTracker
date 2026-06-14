namespace FinanceTracker.Application.Common.Interfaces;

/// <summary>
/// Encapsulates the authenticated caller for the rest of the app, so services
/// and the EF Core global query filter never touch <c>HttpContext</c> directly
/// (ARCHITECTURE.md §11.1). <c>UserId</c> is the GoTrue <c>sub</c> claim;
/// <c>IsAdmin</c> is read strictly from <c>app_metadata.role</c>.
/// </summary>
/// <remarks>
/// The HTTP-bound implementation is wired in T1.2.3; until then a deny-by-default
/// placeholder is registered (see Infrastructure DI).
/// </remarks>
public interface ICurrentUserService
{
    /// <summary>The caller's user id (<c>sub</c>), or <c>null</c> when unauthenticated.</summary>
    Guid? UserId { get; }

    /// <summary>Whether the caller has the admin role (bypasses the data-isolation filter).</summary>
    bool IsAdmin { get; }
}
