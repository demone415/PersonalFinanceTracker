using FinanceTracker.Application.Features.Import;
using Hangfire;

namespace FinanceTracker.Infrastructure.BackgroundJobs;

/// <summary>
/// <see cref="IAccrualImportScheduler"/> backed by Hangfire. The job is routed to
/// the <c>imports</c> queue via the <c>[Queue]</c> attribute on
/// <see cref="AccrualImportJob"/>; the source object key is persisted as a job
/// argument so it survives an app restart before the worker picks it up.
/// </summary>
internal sealed class HangfireAccrualImportScheduler(IBackgroundJobClient client) : IAccrualImportScheduler
{
    public void Enqueue(Guid taskId, string sourceObjectKey) =>
        client.Enqueue<AccrualImportJob>(job => job.RunAsync(taskId, sourceObjectKey, CancellationToken.None));

    public void ScheduleRetry(Guid taskId, string sourceObjectKey, TimeSpan delay) =>
        client.Schedule<AccrualImportJob>(job => job.RunAsync(taskId, sourceObjectKey, CancellationToken.None), delay);
}
