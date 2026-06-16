using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.BackgroundJobs;

/// <summary>
/// Registers the recurring receipt-dispatch sweep (T4.2.2) once the host is up.
/// Runs every few minutes to pick up receipts whose scheduled retry time has
/// arrived and to recover any orphaned by a lost message.
/// </summary>
internal sealed class ReceiptDispatchRecurringInstaller(
    IRecurringJobManager recurringJobs,
    ILogger<ReceiptDispatchRecurringInstaller> logger) : IHostedService
{
    private const string JobId = "receipt-dispatch";

    // Every 5 minutes — well inside the shortest retry delay (6h) and the daily quota.
    private const string CronExpression = "*/5 * * * *";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        recurringJobs.AddOrUpdate<ReceiptDispatchJob>(
            JobId,
            job => job.DispatchAsync(CancellationToken.None),
            CronExpression);

        logger.LogInformation("Registered recurring receipt-dispatch sweep ('{Cron}').", CronExpression);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
