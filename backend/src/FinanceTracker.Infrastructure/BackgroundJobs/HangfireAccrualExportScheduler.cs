using FinanceTracker.Application.Features.Export;
using Hangfire;

namespace FinanceTracker.Infrastructure.BackgroundJobs;

/// <summary>
/// <see cref="IAccrualExportScheduler"/> backed by Hangfire. The job is routed to
/// the <c>exports</c> queue via the <c>[Queue]</c> attribute on
/// <see cref="AccrualExportJob"/>; the filter is persisted as a job argument so
/// it survives an app restart before the worker picks it up.
/// </summary>
internal sealed class HangfireAccrualExportScheduler(IBackgroundJobClient client) : IAccrualExportScheduler
{
    public void Enqueue(Guid taskId, AccrualExportFilter filter) =>
        client.Enqueue<AccrualExportJob>(job => job.RunAsync(taskId, filter, CancellationToken.None));
}
