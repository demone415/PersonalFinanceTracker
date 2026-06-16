using Amazon.Runtime;
using Amazon.S3;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Infrastructure.Caching;
using FinanceTracker.Infrastructure.ExternalProviders.ProverkaChecka;
using FinanceTracker.Infrastructure.Identity;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Persistence.Interceptors;
using FinanceTracker.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace FinanceTracker.Infrastructure;

/// <summary>
/// Composition root for the Infrastructure layer: EF Core, Unit of Work,
/// object storage, readiness probes, and (later) messaging, caching, background
/// jobs and external providers.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' is not configured.");

        services.AddScoped<ChangeLogInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            options.AddInterceptors(sp.GetRequiredService<ChangeLogInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Data-isolation caller context (T1.2.3): reads UserId / IsAdmin from the
        // validated GoTrue JWT on the current HttpContext.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        AddObjectStorage(services, configuration);
        AddCaching(services, configuration);

        // External receipt provider (Story 4.1): Refit client + Polly resilience,
        // and the Redis-backed daily-quota rate limiter (fail-closed).
        services.AddReceiptProvider(configuration);

        AddHealthChecks(services, configuration, connectionString);

        return services;
    }

    /// <summary>
    /// FusionCache (in-memory L1 + Redis L2 distributed cache + Redis backplane)
    /// for dashboard aggregates (T2.1.5). 5-minute TTL with fail-safe so a Redis
    /// outage degrades to memory-only rather than failing requests; cache
    /// stampede protection is built in. Tagging lets us invalidate every cached
    /// aggregate for a user in one call (see <see cref="FusionCacheDashboardCache"/>).
    /// </summary>
    private static void AddCaching(IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";

        services.AddFusionCache()
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromMinutes(5),

                // Fail-safe: serve a stale value (and keep working) if the factory
                // or the distributed cache is briefly unavailable.
                IsFailSafeEnabled = true,
                FailSafeMaxDuration = TimeSpan.FromHours(1),
                FailSafeThrottleDuration = TimeSpan.FromSeconds(30),

                // Cap how long the DB aggregation may block a request before the
                // stale value (if any) is returned and the refresh continues in
                // the background.
                FactorySoftTimeout = TimeSpan.FromMilliseconds(500),

                // Don't let a slow/unavailable Redis stall the request.
                DistributedCacheSoftTimeout = TimeSpan.FromSeconds(1),
                DistributedCacheHardTimeout = TimeSpan.FromSeconds(2),
                AllowBackgroundDistributedCacheOperations = true,
            })
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            .WithDistributedCache(
                new RedisCache(new RedisCacheOptions { Configuration = redisConnection }))
            .WithBackplane(
                new RedisBackplane(new RedisBackplaneOptions { Configuration = redisConnection }));

        services.AddScoped<IDashboardCache, FusionCacheDashboardCache>();
    }

    /// <summary>
    /// Registers the provider-agnostic S3 client (MinIO / AWS S3 / any
    /// S3-compatible store, selected only by the endpoint) and ensures the
    /// private <c>finance-files</c> bucket exists at startup (T1.1.9).
    /// </summary>
    private static void AddObjectStorage(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<S3StorageOptions>()
            .Bind(configuration.GetSection(S3StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<S3StorageOptions>>().Value;
            var config = new AmazonS3Config { ForcePathStyle = options.ForcePathStyle };

            // Empty endpoint ⇒ real AWS (region from environment); otherwise a
            // self-hosted S3 (MinIO) reached over an explicit ServiceURL.
            if (!string.IsNullOrWhiteSpace(options.Endpoint))
            {
                config.ServiceURL = options.Endpoint;
            }

            var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
            return new AmazonS3Client(credentials, config);
        });

        services.AddSingleton<IFileStorage, S3FileStorage>();
        services.AddHostedService<S3BucketInitializer>();
    }

    /// <summary>
    /// Liveness/readiness probes (T1.1.10): a cheap self-check for liveness and
    /// Postgres/Redis/RabbitMQ/MinIO dependency probes for readiness, tagged so
    /// the Api can split them across <c>/health/live</c> and <c>/health/ready</c>.
    /// </summary>
    private static void AddHealthChecks(
        IServiceCollection services,
        IConfiguration configuration,
        string postgresConnectionString)
    {
        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";

        // Lazily-created shared connection used by the RabbitMQ readiness probe.
        // Built on first probe (not at startup), so a down broker never blocks boot.
        services.AddSingleton<IConnection>(_ =>
        {
            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMq:Host"] ?? "localhost",
                Port = int.TryParse(configuration["RabbitMq:Port"], out var port) ? port : 5672,
                UserName = configuration["RabbitMq:Username"] ?? "guest",
                Password = configuration["RabbitMq:Password"] ?? "guest",
                AutomaticRecoveryEnabled = true,
            };
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddNpgSql(postgresConnectionString, name: "postgres", tags: ["ready"])
            .AddRedis(redisConnection, name: "redis", tags: ["ready"])
            .AddRabbitMQ(name: "rabbitmq", tags: ["ready"])
            .AddTypeActivatedCheck<MinioHealthCheck>("minio", HealthStatus.Unhealthy, tags: ["ready"]);
    }
}
