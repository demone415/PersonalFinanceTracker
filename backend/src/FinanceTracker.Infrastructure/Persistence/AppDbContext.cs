using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the application's <c>public.*</c> schema.
/// The <c>auth.*</c> schema is owned by Supabase GoTrue and is never touched here.
/// Entity configurations are discovered from this assembly via
/// <see cref="ModelBuilder.ApplyConfigurationsFromAssembly(System.Reflection.Assembly)"/>.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUser)
    : DbContext(options), IUserIsolatedContext, IApplicationDbContext
{
    /// <summary>The authenticated caller, referenced by the data-isolation query filter.</summary>
    public ICurrentUserService CurrentUser => currentUser;

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Accrual> Accruals => Set<Accrual>();
    public DbSet<AccrualTag> AccrualTags => Set<AccrualTag>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptItem> ReceiptItems => Set<ReceiptItem>();
    public DbSet<ChangeLog> ChangeLogs => Set<ChangeLog>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Data isolation by default (ARCHITECTURE.md §11.2): every user-owned
        // entity is filtered to the current caller; admins bypass via IsAdmin.
        UserIsolationQueryFilter.Apply(modelBuilder, this);

        // Categories are special: a null UserId is a shared system category that
        // every caller can see (but only admins may modify — enforced in the service).
        modelBuilder.Entity<Category>().HasQueryFilter(c =>
            CurrentUser.IsAdmin || c.UserId == null || c.UserId == CurrentUser.UserId);

        base.OnModelCreating(modelBuilder);
    }
}
