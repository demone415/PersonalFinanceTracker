using FinanceTracker.Infrastructure;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logging — Serilog only; sinks and levels are configured in appsettings.json,
// never in code (see CLAUDE.md backend conventions).
builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));

// Infrastructure layer: EF Core / AppDbContext / Unit of Work (and later
// messaging, caching, background jobs, external providers).
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();

// OpenAPI document "v1" — matches the URL-segment API version (/api/v1).
// Additional versions register their own document (e.g. AddOpenApi("v2")).
builder.Services.AddOpenApi("v1");

var app = builder.Build();

app.UseSerilogRequestLogging();

// API docs are a developer tool and are not exposed in production
// (in prod nginx is the only public service — see ARCHITECTURE.md §11.5).
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                       // GET /openapi/v1.json
    app.MapScalarApiReference();            // Scalar UI at /scalar/v1
}

app.MapControllers();

app.MapGet("/", () => "FinanceTracker API");

app.Run();

// Exposed so integration tests can bootstrap the app via WebApplicationFactory<Program>.
public partial class Program;
