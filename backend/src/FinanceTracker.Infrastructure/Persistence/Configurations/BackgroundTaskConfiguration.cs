using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class BackgroundTaskConfiguration : IEntityTypeConfiguration<BackgroundTask>
{
    public void Configure(EntityTypeBuilder<BackgroundTask> builder)
    {
        builder.ToTable("background_tasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Type).IsRequired();
        builder.Property(t => t.Status).IsRequired();
        builder.Property(t => t.Progress).IsRequired();

        // The opaque MinIO object key (256-bit random, Base64Url) and the failure
        // message; both are nullable until the job reaches a terminal state.
        builder.Property(t => t.ResultObjectKey).HasMaxLength(128);
        builder.Property(t => t.Error).HasMaxLength(1000);

        builder.Property(t => t.CreatedAt).IsRequired();

        // The owner's job list, newest first.
        builder.HasIndex(t => new { t.UserId, t.CreatedAt });
    }
}
