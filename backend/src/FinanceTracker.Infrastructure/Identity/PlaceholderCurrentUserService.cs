using FinanceTracker.Application.Common.Interfaces;

namespace FinanceTracker.Infrastructure.Identity;

/// <summary>
/// Deny-by-default placeholder for <see cref="ICurrentUserService"/> used until
/// the HTTP/JWT-bound implementation lands in T1.2.3. Reports no user and no
/// admin, so the global query filter (ARCHITECTURE.md §11.2) hides every
/// user-owned row rather than leaking data before auth exists.
/// </summary>
internal sealed class PlaceholderCurrentUserService : ICurrentUserService
{
    public Guid? UserId => null;

    public bool IsAdmin => false;
}
