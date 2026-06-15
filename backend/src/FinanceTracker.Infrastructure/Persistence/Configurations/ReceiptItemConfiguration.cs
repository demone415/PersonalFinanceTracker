using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class ReceiptItemConfiguration : IEntityTypeConfiguration<ReceiptItem>
{
    public void Configure(EntityTypeBuilder<ReceiptItem> builder)
    {
        builder.ToTable("receipt_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.Name).IsRequired().HasMaxLength(500);
        builder.Property(i => i.Price).IsRequired().HasPrecision(18, 4);
        builder.Property(i => i.Quantity).IsRequired().HasPrecision(12, 3);
        builder.Property(i => i.Sum).IsRequired().HasPrecision(18, 4);

        builder.HasIndex(i => i.ReceiptId);
    }
}
