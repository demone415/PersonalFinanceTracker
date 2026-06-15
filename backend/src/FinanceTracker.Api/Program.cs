using FinanceTracker.Api.Authentication;
using FinanceTracker.Api.Common;
using FinanceTracker.Api.Observability;
using FinanceTracker.Api.RateLimiting;
using FinanceTracker.Application;
using FinanceTracker.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logging — Serilog only; sinks and levels are configured in appsettings.json,
// never in code (see CLAUDE.md backend conventions).
builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));

// Infrastructure layer: EF Core / AppDbContext / Unit of Work, object storage,
// readiness probes (and later messaging, caching, background jobs, providers).
builder.Services.AddInfrastructure(builder.Configuration);

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

// FluentValidation runs via a global action filter (see ValidationFilter).
builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>());

// OpenAPI document "v1" — matches the URL-segment API version (/api/v1).
// Additional versions register their own document (e.g. AddOpenApi("v2")).
builder.Services.AddOpenApi("v1");

var app = builder.Build();

app.UseExceptionHandler();

app.UseSerilogRequestLogging();

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

app.Run();

// Exposed so integration tests can bootstrap the app via WebApplicationFactory<Program>.
public partial class Program;
