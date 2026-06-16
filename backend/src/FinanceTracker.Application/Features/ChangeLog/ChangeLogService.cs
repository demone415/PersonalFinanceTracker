using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Accruals;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Application.Features.ChangeLog;

/// <summary>
/// Read-only feature service for the change log (Epic 7, Story 7.1). The log is
/// written by <c>ChangeLogInterceptor</c>; this service only queries it. Data
/// isolation is enforced by the EF Core global query filter — a regular user sees
/// only their own rows, an admin sees all (audit view), per §11.2.
/// </summary>
public sealed class ChangeLogService(IApplicationDbContext db)
{
    public async Task<PagedResult<ChangeLogDto>> GetPagedAsync(
        ChangeLogFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        var query = db.ChangeLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
            query = query.Where(c => c.EntityType == filter.EntityType);
        if (!string.IsNullOrWhiteSpace(filter.Action))
            query = query.Where(c => c.Action == filter.Action);

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            // Tie-break on Id (GUID v7, time-ordered): rows written in one SaveChanges
            // share a Timestamp, so without it pagination order would be non-deterministic.
            .OrderByDescending(c => c.Timestamp)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ChangeLogDto(
                c.Id,
                c.UserId,
                c.EntityType,
                c.EntityId,
                c.Action,
                c.Timestamp,
                c.ValuesBefore,
                c.ValuesAfter))
            .ToListAsync(cancellationToken);

        return new PagedResult<ChangeLogDto>(items, page, pageSize, totalCount);
    }
}
