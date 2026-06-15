namespace FinanceTracker.Domain.Entities;

/// <summary>
/// Stores completed POST/PUT/DELETE responses so that retries with the same
/// <c>Idempotency-Key</c> header return the cached result instead of re-executing
/// (ARCHITECTURE.md §4 — Idempotency pattern).
/// </summary>
public class IdempotencyRecord
{
    public Guid Id { get; private set; }
    public string Key { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public string RequestMethod { get; private set; } = null!;
    public string RequestPath { get; private set; } = null!;
    public int StatusCode { get; private set; }
    public string ResponseBody { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    private IdempotencyRecord() { }

    public IdempotencyRecord(
        string key,
        Guid userId,
        string requestMethod,
        string requestPath,
        int statusCode,
        string responseBody)
    {
        Id = Guid.CreateVersion7();
        Key = key;
        UserId = userId;
        RequestMethod = requestMethod;
        RequestPath = requestPath;
        StatusCode = statusCode;
        ResponseBody = responseBody;
        CreatedAt = DateTimeOffset.UtcNow;
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(24);
    }
}
