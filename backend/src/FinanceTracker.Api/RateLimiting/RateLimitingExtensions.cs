using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceTracker.Api.RateLimiting;

/// <summary>
/// ASP.NET Core rate limiting (T1.1.11, ARCHITECTURE.md §11.6): a global
/// per-caller limiter on the whole API, plus stricter named policies applied to
/// the receipt-scan and login endpoints. Partitions are keyed by the
/// authenticated user when available, falling back to the client IP.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>Strict policy for <c>POST /scan-qr</c> — protects the shared provider quota from monopolisation.</summary>
    public const string ScanQrPolicy = "scan-qr";

    /// <summary>Strict policy for login — brute-force / credential-stuffing protection.</summary>
    public const string LoginPolicy = "login";

    /// <summary>Tighter policy for <c>GET /jobs/{id}/result</c> — each hit streams a whole file from storage.</summary>
    public const string JobResultPolicy = "job-result";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Global fixed-window limiter for every request, partitioned per caller.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: CallerKey(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // POST /scan-qr — much tighter, since each call ultimately consumes the
            // global ≤15/day ProverkaCheka quota.
            options.AddPolicy(ScanQrPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: CallerKey(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // GET /jobs/{id}/result — streams a file from MinIO through the API, so
            // it's tighter than the generic limit to curb bandwidth abuse.
            options.AddPolicy(JobResultPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: CallerKey(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // Login — keyed by IP (caller is unauthenticated at this point).
            options.AddPolicy(LoginPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ClientIp(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    private static string CallerKey(HttpContext context) =>
        context.User.FindFirst("sub")?.Value
        ?? context.User.Identity?.Name
        ?? ClientIp(context);

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
