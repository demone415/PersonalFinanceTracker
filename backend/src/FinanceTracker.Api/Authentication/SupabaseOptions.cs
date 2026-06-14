using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Api.Authentication;

/// <summary>
/// Binds the <c>Supabase</c> configuration section used to validate GoTrue JWTs
/// offline (ARCHITECTURE.md §11.1). The secret is shared with GoTrue, which
/// signs tokens with it (HS256).
/// </summary>
public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    /// <summary>Shared HS256 signing secret (must match GoTrue's <c>GOTRUE_JWT_SECRET</c>).</summary>
    [Required]
    [MinLength(32)]
    public string JwtSecret { get; set; } = string.Empty;

    /// <summary>Expected <c>iss</c> claim (GoTrue's configured issuer).</summary>
    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Expected <c>aud</c> claim (GoTrue's <c>GOTRUE_JWT_AUD</c>, e.g. <c>authenticated</c>).</summary>
    [Required]
    public string Audience { get; set; } = "authenticated";
}
