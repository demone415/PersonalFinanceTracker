using FinanceTracker.Application.Common.Exceptions;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Application.Features.Accruals;

/// <summary>
/// Feature service for accruals (T1.4.2, T1.4.3). Data isolation via EF global
/// query filters; tags and GroupId are supported natively.
/// </summary>
public sealed class AccrualService(
    IApplicationDbContext db,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IDashboardCache dashboardCache,
    ILogger<AccrualService> logger)
{
    public async Task<PagedResult<AccrualListItemDto>> GetPagedAsync(
        AccrualFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        var query = db.Accruals.AsQueryable();

        if (filter.DateFrom.HasValue)
            query = query.Where(a => a.Date >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue)
            query = query.Where(a => a.Date <= filter.DateTo.Value);
        if (filter.CategoryId.HasValue)
            query = query.Where(a => a.CategoryId == filter.CategoryId.Value);
        if (filter.AmountMin.HasValue)
            query = query.Where(a => a.Amount >= filter.AmountMin.Value);
        if (filter.AmountMax.HasValue)
            query = query.Where(a => a.Amount <= filter.AmountMax.Value);
        if (filter.Type.HasValue)
            query = query.Where(a => a.Type == filter.Type.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(a => a.Date)
            .ThenByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AccrualListItemDto(
                a.Id,
                a.Amount,
                a.Date,
                a.Type,
                a.Currency,
                a.CategoryId,
                a.Category != null ? a.Category.Name : null,
                a.Category != null ? a.Category.Color : null,
                a.Category != null ? a.Category.Icon : null,
                a.Description,
                a.IncludeInStats,
                a.Tags.Select(t => t.Tag).ToList()))
            .ToListAsync(cancellationToken);

        return new PagedResult<AccrualListItemDto>(items, page, pageSize, totalCount);
    }

    public async Task<AccrualDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var a = await db.Accruals
            .Include(x => x.Tags)
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Accrual), id);

        return ToDto(a);
    }

    public async Task<AccrualDto> CreateAsync(
        CreateAccrualRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("Authentication required.");

        await EnsureExchangeRateForForeignCurrencyAsync(
            userId, request.Currency, request.ExchangeRate, cancellationToken);

        var accrual = new Accrual(
            userId,
            request.Amount,
            request.Date,
            request.Type,
            request.Currency,
            request.CategoryId,
            request.Description,
            request.IncludeInStats,
            request.GroupId,
            request.ExchangeRate);

        if (request.Tags is { Count: > 0 })
            accrual.SetTags(request.Tags);

        db.Accruals.Add(accrual);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateDashboardAsync(userId, cancellationToken);

        return await GetByIdAsync(accrual.Id, cancellationToken);
    }

    public async Task<AccrualDto> UpdateAsync(
        Guid id,
        UpdateAccrualRequest request,
        CancellationToken cancellationToken = default)
    {
        var accrual = await LoadOwnedAsync(id, cancellationToken);

        await EnsureExchangeRateForForeignCurrencyAsync(
            accrual.UserId, request.Currency, request.ExchangeRate, cancellationToken);

        accrual.Update(
            request.Amount,
            request.Date,
            request.Type,
            request.Currency,
            request.CategoryId,
            request.Description,
            request.IncludeInStats,
            request.GroupId,
            request.ExchangeRate);

        accrual.SetTags(request.Tags ?? []);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateDashboardAsync(accrual.UserId, cancellationToken);
        return await GetByIdAsync(accrual.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var accrual = await LoadOwnedAsync(id, cancellationToken);
        var ownerId = accrual.UserId;
        db.Accruals.Remove(accrual);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateDashboardAsync(ownerId, cancellationToken);
    }

    // ── Receipt items (T1.4.7) ────────────────────────────────────────────────

    public async Task<ReceiptDto> GetReceiptAsync(
        Guid accrualId,
        CancellationToken cancellationToken = default)
    {
        var accrual = await LoadOwnedAsync(accrualId, cancellationToken);

        if (!accrual.ReceiptId.HasValue)
            throw new NotFoundException(nameof(Receipt), accrualId);

        var receipt = await db.Receipts
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == accrual.ReceiptId.Value, cancellationToken)
            ?? throw new NotFoundException(nameof(Receipt), accrual.ReceiptId.Value);

        return ToReceiptDto(receipt);
    }

    public async Task<ReceiptDto> GetOrCreateReceiptAsync(
        Guid accrualId,
        CancellationToken cancellationToken = default)
    {
        var accrual = await LoadOwnedAsync(accrualId, cancellationToken);

        if (accrual.ReceiptId.HasValue)
        {
            var existing = await db.Receipts
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == accrual.ReceiptId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(Receipt), accrual.ReceiptId.Value);
            return ToReceiptDto(existing);
        }

        var userId = currentUser.UserId!.Value;
        var receipt = Receipt.CreateManual(userId, (long)Math.Round(accrual.Amount * 100m, MidpointRounding.AwayFromZero), accrual.Date);
        db.Receipts.Add(receipt);
        accrual.SetReceipt(receipt.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToReceiptDto(receipt);
    }

    public async Task<ReceiptItemDto> AddReceiptItemAsync(
        Guid accrualId,
        CreateReceiptItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var receipt = await GetOrLoadReceiptAsync(accrualId, cancellationToken);
        var item = new ReceiptItem(receipt.Id, request.Name, request.Price, request.Quantity, request.Sum);
        receipt.AddItem(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ReceiptItemDto(item.Id, item.Name, item.Price, item.Quantity, item.Sum);
    }

    public async Task<ReceiptItemDto> UpdateReceiptItemAsync(
        Guid accrualId,
        Guid itemId,
        UpdateReceiptItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var receipt = await GetOrLoadReceiptAsync(accrualId, cancellationToken);
        var item = receipt.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new NotFoundException(nameof(ReceiptItem), itemId);
        item.Update(request.Name, request.Price, request.Quantity, request.Sum);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ReceiptItemDto(item.Id, item.Name, item.Price, item.Quantity, item.Sum);
    }

    public async Task DeleteReceiptItemAsync(
        Guid accrualId,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var receipt = await GetOrLoadReceiptAsync(accrualId, cancellationToken);
        var item = receipt.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new NotFoundException(nameof(ReceiptItem), itemId);
        receipt.RemoveItem(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Enforces the currency-aggregation contract at the source of truth: a
    /// transaction in a currency other than the owner's base currency must carry an
    /// exchange rate, otherwise <see cref="Accrual.AmountInBaseCurrency"/> would
    /// silently treat it as 1:1 and skew every aggregate. The frontend enforces the
    /// same rule, but the API is the source of truth (e.g. direct/imported writes).
    /// </summary>
    private async Task EnsureExchangeRateForForeignCurrencyAsync(
        Guid ownerId, string currency, decimal? exchangeRate, CancellationToken cancellationToken)
    {
        if (exchangeRate is > 0m)
            return;

        var baseCurrency = await db.UserProfiles
            .Where(p => p.Id == ownerId)
            .Select(p => p.Currency)
            .FirstOrDefaultAsync(cancellationToken) ?? "RUB";

        if (!string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException(
                $"An exchange rate to the base currency ({baseCurrency}) is required for {currency} transactions.");
    }

    /// <summary>
    /// Drops the owner's cached dashboard aggregates after a committed write.
    /// Best-effort: a cache failure must never fail a write that already
    /// succeeded — the entry self-heals on its 5-minute TTL.
    /// </summary>
    private async Task InvalidateDashboardAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await dashboardCache.InvalidateAsync(userId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to invalidate dashboard cache for user {UserId}; it will expire on TTL.",
                userId);
        }
    }

    private async Task<Accrual> LoadOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        var accrual = await db.Accruals
            .Include(a => a.Tags)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Accrual), id);

        if (!currentUser.IsAdmin && accrual.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not own this accrual.");

        return accrual;
    }

    private async Task<Receipt> GetOrLoadReceiptAsync(Guid accrualId, CancellationToken cancellationToken)
    {
        var receiptDto = await GetOrCreateReceiptAsync(accrualId, cancellationToken);
        var receipt = await db.Receipts
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == receiptDto.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Receipt), receiptDto.Id);
        return receipt;
    }

    private static AccrualDto ToDto(Accrual a) => new(
        a.Id,
        a.UserId,
        a.Amount,
        a.Date,
        a.Type,
        a.Currency,
        a.ExchangeRate,
        a.CategoryId,
        a.Category?.Name,
        a.Category?.Color,
        a.Category?.Icon,
        a.Description,
        a.IncludeInStats,
        a.GroupId,
        a.ReceiptId,
        a.Tags.Select(t => t.Tag).ToList(),
        a.CreatedAt);

    private static ReceiptDto ToReceiptDto(Receipt r) => new(
        r.Id,
        r.AmountInKopecks,
        r.Date,
        r.Organization,
        r.Address,
        r.INN,
        r.FetchStatus.ToString(),
        r.FetchAttempts,
        r.Items.Select(i => new ReceiptItemDto(i.Id, i.Name, i.Price, i.Quantity, i.Sum)).ToList());
}
