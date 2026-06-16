using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        builder.ToTable("receipts");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.AmountInKopecks).IsRequired();
        builder.Property(r => r.Date).IsRequired();
        builder.Property(r => r.FN).HasMaxLength(16);
        builder.Property(r => r.FPD).HasMaxLength(16);
        builder.Property(r => r.INN).HasMaxLength(12);
        builder.Property(r => r.Cashier).HasMaxLength(200);
        builder.Property(r => r.Organization).HasMaxLength(200);
        builder.Property(r => r.Address).HasMaxLength(500);
        builder.Property(r => r.ExternalNumber).HasMaxLength(32);
        builder.Property(r => r.FetchStatus).IsRequired();
        builder.Property(r => r.FetchAttempts).IsRequired();
        builder.Property(r => r.QrRaw).HasMaxLength(512);

        builder.HasMany(r => r.Items)
            .WithOne()
            .HasForeignKey(i => i.ReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.UserId);

        // The dispatcher polls for due work: Pending receipts ordered by when the
        // next attempt is allowed. A composite index keeps that scan cheap.
        builder.HasIndex(r => new { r.FetchStatus, r.NextFetchAt });
    }
}
