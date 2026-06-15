using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class AccrualConfiguration : IEntityTypeConfiguration<Accrual>
{
    public void Configure(EntityTypeBuilder<Accrual> builder)
    {
        builder.ToTable("accruals");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Amount).IsRequired().HasPrecision(18, 4);
        builder.Property(a => a.Date).IsRequired();
        builder.Property(a => a.Type).IsRequired();
        builder.Property(a => a.Currency).IsRequired().HasMaxLength(3);
        builder.Property(a => a.ExchangeRate).HasPrecision(18, 6);
        builder.Property(a => a.Description).HasMaxLength(500);
        builder.Property(a => a.IncludeInStats).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        // Indexes per ARCHITECTURE.md §11.3
        builder.HasIndex(a => new { a.UserId, a.Date });
        builder.HasIndex(a => a.CategoryId);
        builder.HasIndex(a => a.Type);
        builder.HasIndex(a => a.GroupId);

        builder.HasMany(a => a.Tags)
            .WithOne()
            .HasForeignKey(t => t.AccrualId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Category)
            .WithMany()
            .HasForeignKey(a => a.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.Receipt)
            .WithOne()
            .HasForeignKey<Accrual>(a => a.ReceiptId)
            .OnDelete(DeleteBehavior.SetNull);

        // Optimistic locking via PostgreSQL xmin system column (ARCHITECTURE.md §4).
        // xmin is a PG system column (type xid → uint); no migration column is added.
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .HasColumnName("xmin")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
