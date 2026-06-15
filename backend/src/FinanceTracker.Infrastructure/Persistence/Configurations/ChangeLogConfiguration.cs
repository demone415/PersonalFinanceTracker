using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class ChangeLogConfiguration : IEntityTypeConfiguration<ChangeLog>
{
    public void Configure(EntityTypeBuilder<ChangeLog> builder)
    {
        builder.ToTable("change_logs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.EntityType).IsRequired().HasMaxLength(64);
        builder.Property(c => c.Action).IsRequired().HasMaxLength(16);
        builder.Property(c => c.Timestamp).IsRequired();

        builder.HasIndex(c => new { c.UserId, c.Timestamp });
        builder.HasIndex(c => new { c.EntityType, c.EntityId });
    }
}
