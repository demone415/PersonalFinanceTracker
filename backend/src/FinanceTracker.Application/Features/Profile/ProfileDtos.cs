namespace FinanceTracker.Application.Features.Profile;

/// <summary>
/// The caller's application profile (Epic 8, T8.1.2). <see cref="Currency"/> is
/// the user's base currency — the unit every aggregate (dashboard, budget
/// progress) is reported in after per-transaction conversion.
/// </summary>
public sealed record ProfileDto(
    Guid Id,
    string? DisplayName,
    string Currency);

/// <summary>Editable profile fields: display name and base currency.</summary>
public sealed record UpdateProfileRequest(
    string? DisplayName,
    string Currency);
