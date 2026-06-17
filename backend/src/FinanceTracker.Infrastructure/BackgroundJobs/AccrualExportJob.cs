using FinanceTracker.Application.Features.Export;
using Hangfire;

namespace FinanceTracker.Infrastructure.BackgroundJobs;

/// <summary>
/// The Hangfire entry point for a CSV export (T6.2.1). It runs on the dedicated
/// <c>exports</c> queue — separate from the strictly sequential single-worker
/// <c>receipts</c> queue — and merely delegates to the transport-agnostic
/// <see cref="AccrualExportProcessor"/>, which owns the whole (idempotent) flow.
/// </summary>
[Queue(ExportQueues.Exports)]
public sealed class AccrualExportJob(AccrualExportProcessor processor)
{
    public Task RunAsync(Guid taskId, AccrualExportFilter filter, CancellationToken cancellationToken) =>
        processor.ProcessAsync(taskId, filter, cancellationToken);
}
