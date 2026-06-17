using FinanceTracker.Api.Authentication;
using FinanceTracker.Api.Common;
using FinanceTracker.Api.Observability;
using FinanceTracker.Api.RateLimiting;
using FinanceTracker.Application;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Messaging;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using Serilog;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

// Logging — Serilog only; sinks and levels are configured in appsettings.json,
// never in code (see CLAUDE.md backend conventions).
builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));

// Infrastructure layer: EF Core / AppDbContext / Unit of Work, object storage,
// caching, background jobs (Hangfire), readiness probes and external providers.
builder.Services.AddInfrastructure(builder.Configuration);

// Message bus (Story 4.2): Wolverine over RabbitMQ — the receipt-fetch consumer,
// explicit routing and the dead-letter policy (see WolverineConfiguration).
builder.Host.UseWolverine(options =>
    WolverineConfiguration.Configure(options, builder.Configuration));

// Application layer: feature services + FluentValidation validators.
builder.Services.AddApplication();

// GoTrue JWT validation (T1.2.2): offline HS256 validation by the shared secret.
builder.Services.AddGoTrueJwtAuthentication(builder.Configuration);

// ProblemDetails responses + map application exceptions (404/403) to them.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Cross-cutting API concerns: rate limiting (T1.1.11) and metrics (T1.1.13).
builder.Services.AddApiRateLimiting();
builder.Services.AddObservability();

// CORS for the SPA. Outside production the frontend (e.g. :3000 / Vite :5173)
// is a different origin than the API (:5000), so browser preflight needs it.
// In production nginx makes them same-origin (ARCHITECTURE.md §11.5).
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000", "http://localhost:5173"];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        // Let the SPA read the download filename of streamed job results
        // (GET /jobs/{id}/result) cross-origin in dev.
        .WithExposedHeaders("Content-Disposition")));

// FluentValidation runs via a global action filter (see ValidationFilter).
// Idempotency-Key deduplication runs via IdempotencyFilter (ARCHITECTURE.md §4).
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
    options.Filters.Add<IdempotencyFilter>();
})
.AddJsonOptions(options =>
{
    // Serialize enums as their string names (e.g. "Income", not 0) so the SPA's
    // string union types (AccrualType, …) match the wire format. Without this the
    // default integer enums break client-side type checks (e.g. income rendered
    // as an outflow).
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// OpenAPI document "v1" — matches the URL-segment API version (/api/v1).
// Additional versions register their own document (e.g. AddOpenApi("v2")).
builder.Services.AddOpenApi("v1");

var app = builder.Build();

app.UseExceptionHandler();

app.UseSerilogRequestLogging();

// CORS must run before auth/rate limiter so the unauthenticated preflight
// OPTIONS is answered (and not rate-limited or rejected as 405/401).
app.UseCors();

// Authentication must run before the rate limiter, which partitions by the
// authenticated user (sub claim) when present.
app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

// API docs are a developer tool and are not exposed in production
// (in prod nginx is the only public service — see ARCHITECTURE.md §11.5).
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                       // GET /openapi/v1.json
    app.MapScalarApiReference();            // Scalar UI at /scalar/v1
}

app.MapControllers();

// Liveness vs readiness split by health-check tag (T1.1.10, ARCHITECTURE.md §11.5).
// Liveness = process is up; readiness = Postgres/Redis/RabbitMQ/MinIO reachable.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

// Prometheus scraping endpoint (T1.1.13): GET /metrics.
app.MapPrometheusScrapingEndpoint();

app.MapGet("/", () => "FinanceTracker API");

// Run EF migrations and seed initial data before accepting requests.
await DatabaseSeeder.SeedAsync(
    app.Configuration,
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder"));

app.Run();

// Exposed so integration tests can bootstrap the app via WebApplicationFactory<Program>.
public partial class Program;
