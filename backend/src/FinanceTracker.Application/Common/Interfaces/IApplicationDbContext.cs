using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Application.Common.Interfaces;

/// <summary>
/// Read/write access to the application's aggregates for feature services,
/// without exposing the concrete EF Core context. Persistence is committed
/// through <see cref="IUnitOfWork"/>; entities are filtered by the data-isolation
/// query filters configured on the context (§11.2).
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<Accrual> Accruals { get; }
    DbSet<AccrualTag> AccrualTags { get; }
    DbSet<Receipt> Receipts { get; }
    DbSet<ReceiptItem> ReceiptItems { get; }
    DbSet<ChangeLog> ChangeLogs { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }
}
