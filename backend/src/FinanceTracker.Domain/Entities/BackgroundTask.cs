using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Domain.Entities;

/// <summary>
/// Tracks the state of an asynchronous import/export job (ARCHITECTURE.md
/// §BackgroundTask, Epic 6). User-owned, so it is subject to the data-isolation
/// global query filter. The result of a finished job lives in the private
/// <c>finance-files</c> bucket under the opaque, cryptographically random
/// <see cref="ResultObjectKey"/> — never exposed to the client; the file is
/// served only by streaming through <c>GET /jobs/{id}/result</c>.
/// </summary>
public class BackgroundTask : IUserOwnedEntity
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public BackgroundTaskType Type { get; private set; }
    public BackgroundTaskStatus Status { get; private set; }

    /// <summary>Coarse progress in the range 0–100.</summary>
    public int Progress { get; private set; }

    /// <summary>
    /// Opaque, cryptographically random key (256-bit) of the result object in
    /// MinIO; <c>null</c> until the job completes. Never returned to the client.
    /// </summary>
    public string? ResultObjectKey { get; private set; }

    /// <summary>Failure detail when <see cref="Status"/> is <see cref="BackgroundTaskStatus.Failed"/>.</summary>
    public string? Error { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private BackgroundTask() { }

    public BackgroundTask(Guid userId, BackgroundTaskType type)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        Type = type;
        Status = BackgroundTaskStatus.Pending;
        Progress = 0;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Marks the job as picked up by a worker (Pending → Running).</summary>
    public void Start()
    {
        if (Status != BackgroundTaskStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot start a background task in state {Status}; only Pending is startable.");

        Status = BackgroundTaskStatus.Running;
    }

    /// <summary>Updates coarse progress (clamped to 0–100) while the job runs.</summary>
    public void ReportProgress(int percent) =>
        Progress = Math.Clamp(percent, 0, 100);

    /// <summary>Completes the job: the result is available under <paramref name="resultObjectKey"/>.</summary>
    public void Complete(string resultObjectKey, DateTimeOffset completedAt)
    {
        if (string.IsNullOrWhiteSpace(resultObjectKey))
            throw new ArgumentException("A completed task must carry a result object key.", nameof(resultObjectKey));

        Status = BackgroundTaskStatus.Done;
        Progress = 100;
        ResultObjectKey = resultObjectKey;
        CompletedAt = completedAt;
    }

    /// <summary>Marks the job as failed with a human-readable <paramref name="error"/>.</summary>
    public void Fail(string error, DateTimeOffset completedAt)
    {
        Status = BackgroundTaskStatus.Failed;
        Error = error;
        CompletedAt = completedAt;
    }
}
