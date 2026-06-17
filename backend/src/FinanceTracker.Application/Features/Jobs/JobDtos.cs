namespace FinanceTracker.Application.Features.Jobs;

/// <summary>Public status of an async import/export job (<c>GET /jobs/{id}</c>).</summary>
public sealed record BackgroundTaskStatusDto(
    Guid Id,
    string Type,
    string Status,
    int Progress,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    bool HasResult);

/// <summary>
/// A ready-to-stream job result (<c>GET /jobs/{id}/result</c>). The caller owns
/// <see cref="Content"/> and must dispose it after streaming to the response.
/// </summary>
public sealed record JobResultStream(
    Stream Content,
    string ContentType,
    string FileName);
