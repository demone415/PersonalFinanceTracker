using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FinanceTracker.Api.Authentication;

/// <summary>
/// GoTrue JWT validation (T1.2.2, ARCHITECTURE.md §11.1). Validates the shared
/// HS256 signature, issuer, audience and lifetime offline — no GoTrue call per
/// request. The algorithm is pinned to HS256 so <c>alg: none</c> / algorithm
/// substitution is rejected. The app role is read from <c>app_metadata.role</c>
/// downstream (never <c>user_metadata</c>) — see CurrentUserService.
/// </summary>
public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddGoTrueJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SupabaseOptions>()
            .Bind(configuration.GetSection(SupabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var options = configuration.GetSection(SupabaseOptions.SectionName).Get<SupabaseOptions>()
            ?? throw new InvalidOperationException("The 'Supabase' configuration section is missing.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                // Keep raw claim names (sub, app_metadata) instead of the legacy
                // SOAP-style mapping, so claim lookups match the GoTrue payload.
                jwt.MapInboundClaims = false;

                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtSecret)),

                    // Pin HS256 — reject alg:none and asymmetric-key substitution.
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),

                    NameClaimType = "sub",
                    RoleClaimType = "role",
                };
            });

        services.AddAuthorization();

        return services;
    }
}
