using System.Security.Cryptography;
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
    /// Opaque, cryptographically random key (256-bit, Base64Url) of the result
    /// object in MinIO. Assigned once at creation and <b>stable for the life of the
    /// task</b>, so a retried/resumed run overwrites the same object instead of
    /// orphaning a fresh one. It is random — deliberately <b>not</b> derived from
    /// the (enumerable, time-ordered) <see cref="Id"/> — so it remains the
    /// file-unguessability layer (CLAUDE.md: ids are enumerable and not secrets).
    /// Never returned to the client; downloadable only once <see cref="Status"/> is
    /// <see cref="BackgroundTaskStatus.Done"/>.
    /// </summary>
    public string ResultObjectKey { get; private set; } = null!;

    /// <summary>Failure detail when <see cref="Status"/> is <see cref="BackgroundTaskStatus.Failed"/>.</summary>
    public string? Error { get; private set; }

    /// <summary>How many processing attempts have been made (incremented on each failed attempt).</summary>
    public int Attempts { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Maximum processing attempts before the job is marked terminally Failed.</summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// Back-off before the 2nd…Nth attempt. With <see cref="MaxAttempts"/> = 3 there
    /// are two delays, so a job that keeps failing is tried at most three times
    /// before it is terminally Failed.
    /// </summary>
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
    ];

    private BackgroundTask() { }

    public BackgroundTask(Guid userId, BackgroundTaskType type)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        Type = type;
        Status = BackgroundTaskStatus.Pending;
        Progress = 0;
        Attempts = 0;
        ResultObjectKey = NewObjectKey(type);
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

    /// <summary>Records a failed processing attempt (used to drive the retry budget).</summary>
    public void RecordAttempt() => Attempts++;

    /// <summary>Whether another attempt is allowed under <see cref="MaxAttempts"/>.</summary>
    public bool HasAttemptsRemaining => Attempts < MaxAttempts;

    /// <summary>
    /// The wait before the next attempt given the attempts made so far, or
    /// <c>null</c> when the retry budget is exhausted (caller should mark Failed).
    /// </summary>
    public TimeSpan? GetNextRetryDelay()
    {
        var index = Attempts - 1; // delay that follows the attempt just made
        return index >= 0 && index < RetryDelays.Length ? RetryDelays[index] : null;
    }

    /// <summary>Completes the job: the result is available under <see cref="ResultObjectKey"/>.</summary>
    public void Complete(DateTimeOffset completedAt)
    {
        Status = BackgroundTaskStatus.Done;
        Progress = 100;
        CompletedAt = completedAt;
    }

    /// <summary>Marks the job as terminally failed with a human-readable <paramref name="error"/>.</summary>
    public void Fail(string error, DateTimeOffset completedAt)
    {
        Status = BackgroundTaskStatus.Failed;
        Error = error;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// A cryptographically random, opaque 256-bit object key (URL-safe Base64, no
    /// padding) under a per-type prefix. The key — never the GUID id — is what
    /// makes the stored file unguessable.
    /// </summary>
    private static string NewObjectKey(BackgroundTaskType type)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var (prefix, extension) = type switch
        {
            BackgroundTaskType.ExportCsv => ("exports", "csv"),
            // The import result is a JSON summary (counts + warnings); the source
            // .xlsx is stored separately under its own key.
            BackgroundTaskType.ImportFns => ("imports", "json"),
            _ => ("files", "bin"),
        };

        return $"{prefix}/{token}.{extension}";
    }
}
