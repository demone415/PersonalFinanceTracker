namespace FinanceTracker.Application.Features.ChangeLog;

/// <summary>
/// A single audit-log row (Epic 7). <c>ValuesBefore</c>/<c>ValuesAfter</c> are raw
/// JSON snapshots captured by the EF interceptor — null on create/delete respectively.
/// </summary>
public sealed record ChangeLogDto(
    Guid Id,
    Guid UserId,
    string EntityType,
    Guid EntityId,
    string Action,
    DateTimeOffset Timestamp,
    string? ValuesBefore,
    string? ValuesAfter);

/// <summary>
/// Query for the change log: paginated, optionally filtered by entity type and action
/// (T7.1.1). Page/PageSize mirror the accrual list contract.
/// </summary>
public sealed record ChangeLogFilterRequest(
    int Page = 1,
    int PageSize = 20,
    string? EntityType = null,
    string? Action = null);
