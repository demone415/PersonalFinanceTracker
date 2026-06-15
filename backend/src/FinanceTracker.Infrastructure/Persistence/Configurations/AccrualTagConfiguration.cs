using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class AccrualTagConfiguration : IEntityTypeConfiguration<AccrualTag>
{
    public void Configure(EntityTypeBuilder<AccrualTag> builder)
    {
        builder.ToTable("accrual_tags");
        builder.HasKey(t => new { t.AccrualId, t.Tag });
        builder.Property(t => t.Tag).IsRequired().HasMaxLength(64);
        builder.HasIndex(t => t.Tag);
    }
}
