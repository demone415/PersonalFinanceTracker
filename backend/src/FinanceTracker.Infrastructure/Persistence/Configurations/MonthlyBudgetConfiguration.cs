using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="MonthlyBudget"/> to <c>public.monthly_budgets</c> (Epic 5).
/// A user may hold at most one budget per category and month, enforced by a
/// unique index. The owner FK <c>user_id → auth.users.id</c> is added in the
/// migration via raw SQL (auth.users is GoTrue-owned, not in the EF model).
/// </summary>
internal sealed class MonthlyBudgetConfiguration : IEntityTypeConfiguration<MonthlyBudget>
{
    public void Configure(EntityTypeBuilder<MonthlyBudget> builder)
    {
        builder.ToTable("monthly_budgets");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.UserId).IsRequired();
        builder.Property(b => b.CategoryId).IsRequired();
        builder.Property(b => b.Year).IsRequired();
        builder.Property(b => b.Month).IsRequired();
        builder.Property(b => b.LimitAmount).IsRequired().HasPrecision(18, 4);
        builder.Property(b => b.Currency).IsRequired().HasMaxLength(3);

        // One budget per user / category / month.
        builder.HasIndex(b => new { b.UserId, b.CategoryId, b.Year, b.Month }).IsUnique();

        builder.HasOne(b => b.Category)
            .WithMany()
            .HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optimistic locking via PostgreSQL xmin system column (ARCHITECTURE.md §4).
        // xmin is a PG system column (type xid → uint); no migration column is added.
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .HasColumnName("xmin")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
