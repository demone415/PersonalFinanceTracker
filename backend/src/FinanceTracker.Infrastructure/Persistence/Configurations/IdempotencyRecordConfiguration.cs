using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Key).IsRequired().HasMaxLength(256);
        builder.Property(r => r.RequestMethod).IsRequired().HasMaxLength(10);
        builder.Property(r => r.RequestPath).IsRequired().HasMaxLength(500);
        builder.Property(r => r.ResponseBody).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.ExpiresAt).IsRequired();

        // Lookup index — the filter queries by (Key, UserId, RequestPath)
        builder.HasIndex(r => new { r.Key, r.UserId, r.RequestPath }).IsUnique();

        // TTL cleanup: rows are expired by ExpiresAt; add index for batch deletes
        builder.HasIndex(r => r.ExpiresAt);
    }
}
