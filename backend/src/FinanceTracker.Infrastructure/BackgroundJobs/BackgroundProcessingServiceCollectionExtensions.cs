using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Export;
using FinanceTracker.Application.Features.Import;
using FinanceTracker.Application.Features.Receipts;
using FinanceTracker.Infrastructure.Messaging;
using FinanceTracker.Infrastructure.Persistence.Import;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Infrastructure.BackgroundJobs;

/// <summary>
/// Registers the receipt background-processing stack (Story 4.2): Hangfire with
/// PostgreSQL persistence, a single-worker server bound to the global FIFO
/// <c>receipts</c> queue, the jobs, and the Wolverine-backed scheduler / DLQ /
/// publisher abstractions. The Wolverine bus itself is configured on the host
/// (see <see cref="WolverineConfiguration"/>).
/// </summary>
public static class BackgroundProcessingServiceCollectionExtensions
{
    public static IServiceCollection AddBackgroundProcessing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));

        // The global FIFO receipts queue: exactly one worker → strictly sequential,
        // never parallel (ARCHITECTURE.md §4 / T4.2.2). No dashboard is exposed.
        services.AddHangfireServer(options =>
        {
            options.ServerName = "receipts";
            options.Queues = [ReceiptQueues.Hangfire];
            options.WorkerCount = 1;
        });

        // A separate server for async import/export (Epic 6): kept off the
        // single-worker receipts queue so a long export/import never blocks receipt
        // fetching, and these jobs may run a few at a time.
        services.AddHangfireServer(options =>
        {
            options.ServerName = "import-export";
            options.Queues = [ExportQueues.Exports, ImportQueues.Imports];
            options.WorkerCount = 2;
        });

        services.AddScoped<ReceiptFetchJob>();
        services.AddScoped<ReceiptDispatchJob>();
        services.AddScoped<AccrualExportJob>();
        services.AddScoped<IAccrualExportScheduler, HangfireAccrualExportScheduler>();

        // Async FNS import (Story 6.1): the Hangfire job, the Application-facing
        // scheduler, and the ClosedXML-backed workbook parser.
        services.AddScoped<AccrualImportJob>();
        services.AddScoped<IAccrualImportScheduler, HangfireAccrualImportScheduler>();
        services.AddSingleton<IFnsReceiptParser, ClosedXmlFnsReceiptParser>();

        // Wolverine/Hangfire-backed implementations of the Application abstractions.
        services.AddScoped<IReceiptFetchScheduler, HangfireReceiptFetchScheduler>();
        services.AddScoped<IReceiptDeadLetterQueue, WolverineReceiptDeadLetterQueue>();
        services.AddScoped<IMessagePublisher, WolverineMessagePublisher>();

        services.AddHostedService<ReceiptDispatchRecurringInstaller>();

        return services;
    }
}
