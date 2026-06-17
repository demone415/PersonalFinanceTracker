namespace FinanceTracker.Domain.Enums;

/// <summary>
/// Lifecycle of an async import/export job (ARCHITECTURE.md §BackgroundTask):
/// <c>Pending → Running → Done | Failed</c>. <c>Done</c> and <c>Failed</c> are
/// terminal.
/// </summary>
public enum BackgroundTaskStatus
{
    /// <summary>Created and queued, not yet picked up by a worker.</summary>
    Pending = 0,

    /// <summary>A worker is currently processing the job.</summary>
    Running = 1,

    /// <summary>Finished successfully; the result is available in object storage.</summary>
    Done = 2,

    /// <summary>Terminated with an error (see <see cref="Entities.BackgroundTask.Error"/>).</summary>
    Failed = 3,
}
