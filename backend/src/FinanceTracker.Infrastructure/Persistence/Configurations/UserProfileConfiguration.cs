using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="UserProfile"/> to <c>public.user_profiles</c>. The DB-level
/// FK <c>id → auth.users.id</c> is added in the migration via raw SQL, since the
/// GoTrue-owned <c>auth.users</c> table is not part of the EF model.
/// </summary>
internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles");

        builder.HasKey(p => p.Id);

        // Id mirrors auth.users.id — supplied by GoTrue, never app-generated.
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.DisplayName).HasMaxLength(200);

        builder.Property(p => p.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasDefaultValue("RUB");

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        // Optimistic locking for this editable entity (CLAUDE.md, ARCHITECTURE.md §4):
        // use the PostgreSQL xmin system column as the concurrency token. No column
        // is added to the table — xmin already exists on every row.
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .HasColumnName("xmin")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
