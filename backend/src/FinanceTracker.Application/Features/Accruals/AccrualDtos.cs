using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Features.Accruals;

public sealed record AccrualDto(
    Guid Id,
    Guid UserId,
    decimal Amount,
    DateTimeOffset Date,
    AccrualType Type,
    string Currency,
    decimal? ExchangeRate,
    Guid? CategoryId,
    string? CategoryName,
    string? CategoryColor,
    string? CategoryIcon,
    string? Description,
    bool IncludeInStats,
    Guid? GroupId,
    Guid? ReceiptId,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt);

public sealed record AccrualListItemDto(
    Guid Id,
    decimal Amount,
    DateTimeOffset Date,
    AccrualType Type,
    string Currency,
    Guid? CategoryId,
    string? CategoryName,
    string? CategoryColor,
    string? CategoryIcon,
    string? Description,
    bool IncludeInStats,
    IReadOnlyList<string> Tags);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed record CreateAccrualRequest(
    decimal Amount,
    DateTimeOffset Date,
    AccrualType Type,
    string Currency,
    Guid? CategoryId,
    string? Description,
    bool IncludeInStats,
    Guid? GroupId,
    decimal? ExchangeRate,
    IReadOnlyList<string> Tags);

public sealed record UpdateAccrualRequest(
    decimal Amount,
    DateTimeOffset Date,
    AccrualType Type,
    string Currency,
    Guid? CategoryId,
    string? Description,
    bool IncludeInStats,
    Guid? GroupId,
    decimal? ExchangeRate,
    IReadOnlyList<string> Tags);

public sealed record AccrualFilterRequest(
    int Page = 1,
    int PageSize = 20,
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null,
    Guid? CategoryId = null,
    decimal? AmountMin = null,
    decimal? AmountMax = null,
    AccrualType? Type = null);

// Receipt DTOs
public sealed record ReceiptDto(
    Guid Id,
    long AmountInKopecks,
    DateTimeOffset Date,
    string? Organization,
    string? Address,
    string? INN,
    string FetchStatus,
    int FetchAttempts,
    IReadOnlyList<ReceiptItemDto> Items);

public sealed record ReceiptItemDto(
    Guid Id,
    string Name,
    decimal Price,
    decimal Quantity,
    decimal Sum);

public sealed record CreateReceiptItemRequest(
    string Name,
    decimal Price,
    decimal Quantity,
    decimal Sum);

public sealed record UpdateReceiptItemRequest(
    string Name,
    decimal Price,
    decimal Quantity,
    decimal Sum);
