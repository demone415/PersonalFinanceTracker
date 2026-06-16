using FinanceTracker.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Refit;
using StackExchange.Redis;

namespace FinanceTracker.Infrastructure.ExternalProviders.ProverkaChecka;

/// <summary>
/// Composition for the ПроверкаЧека integration (Story 4.1): the Refit client
/// wrapped in a Polly resilience pipeline (retry + circuit breaker + timeout),
/// the receipt provider, and the Redis-backed daily-quota rate limiter.
/// </summary>
public static class ReceiptProviderServiceCollectionExtensions
{
    public static IServiceCollection AddReceiptProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ProverkaCheckaOptions>()
            .Bind(configuration.GetSection(ProverkaCheckaOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var providerOptions = configuration.GetSection(ProverkaCheckaOptions.SectionName)
            .Get<ProverkaCheckaOptions>() ?? new ProverkaCheckaOptions();

        // Shared Redis connection for the rate limiter. AbortOnConnectFail=false so a
        // down Redis never blocks startup — the limiter then fails closed at call time.
        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.TryAddSingleton<IConnectionMultiplexer>(_ =>
        {
            var config = ConfigurationOptions.Parse(redisConnection);
            config.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(config);
        });

        services.AddScoped<IReceiptRateLimiter, RedisReceiptRateLimiter>();

        // Refit client → typed HttpClient with the provider base address, then the
        // resilience pipeline. The pipeline's defaults already treat 5xx/408/timeouts
        // and HttpRequestException as transient (HttpClientResiliencePredicates).
        services.AddRefitClient<IProverkaCheckaApi>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(providerOptions.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(providerOptions.RequestTimeoutSeconds + 5);
            })
            .AddResilienceHandler("proverka-checka", (builder, context) =>
            {
                var opts = context.ServiceProvider
                    .GetRequiredService<IOptions<ProverkaCheckaOptions>>().Value;

                builder
                    .AddRetry(new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        Delay = TimeSpan.FromSeconds(1),
                    })
                    .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                    {
                        FailureRatio = 0.5,
                        SamplingDuration = TimeSpan.FromSeconds(30),
                        MinimumThroughput = 5,
                        BreakDuration = TimeSpan.FromSeconds(15),
                    })
                    .AddTimeout(TimeSpan.FromSeconds(opts.RequestTimeoutSeconds));
            });

        services.AddScoped<IReceiptProvider, ProverkaCheckaProvider>();

        return services;
    }
}
