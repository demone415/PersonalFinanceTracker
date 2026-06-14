using OpenTelemetry.Metrics;

namespace FinanceTracker.Api.Observability;

/// <summary>
/// OpenTelemetry metrics → Prometheus (T1.1.13, ARCHITECTURE.md §11.7). Collects
/// ASP.NET Core request metrics and .NET runtime metrics now; domain meters
/// (receipt-queue length, provider quota, async-task duration) register against
/// <see cref="MeterName"/> as those features land.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>Meter name for custom application metrics added in later milestones.</summary>
    public const string MeterName = "FinanceTracker";

    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(MeterName)
                .AddPrometheusExporter());

        return services;
    }
}
