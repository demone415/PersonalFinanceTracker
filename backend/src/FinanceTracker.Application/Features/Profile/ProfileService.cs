using FinanceTracker.Application.Common.Exceptions;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Application.Features.Profile;

/// <summary>
/// Feature service for the caller's own application profile (Epic 8, T8.1.2):
/// reading and updating the base currency (and display name). A
/// <see cref="UserProfile"/> is identified by the GoTrue user id, so reads are
/// scoped by <c>Id == currentUser.UserId</c> explicitly — the profile carries no
/// <c>UserId</c> and is not covered by the global isolation filter. Because users
/// are provisioned via the GoTrue Admin API, the profile row is created lazily on
/// first access if the seeder never ran for that user.
/// </summary>
public sealed class ProfileService(
    IApplicationDbContext db,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
{
    public async Task<ProfileDto> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var profile = await LoadOrCreateAsync(cancellationToken);
        return ToDto(profile);
    }

    public async Task<ProfileDto> UpdateAsync(
        UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await LoadOrCreateAsync(cancellationToken);
        profile.Update(request.DisplayName, Normalize(request.Currency));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(profile);
    }

    private async Task<UserProfile> LoadOrCreateAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("Authentication is required to access the profile.");

        var profile = await db.UserProfiles
            .FirstOrDefaultAsync(p => p.Id == userId, cancellationToken);

        if (profile is null)
        {
            profile = new UserProfile(userId);
            db.UserProfiles.Add(profile);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return profile;
    }

    // ISO-4217 codes are upper-case; store them canonically regardless of input.
    private static string Normalize(string currency) => currency.Trim().ToUpperInvariant();

    private static ProfileDto ToDto(UserProfile p) => new(p.Id, p.DisplayName, p.Currency);
}
