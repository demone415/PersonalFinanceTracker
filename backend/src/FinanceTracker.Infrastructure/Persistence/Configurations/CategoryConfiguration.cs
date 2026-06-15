using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Category"/> to <c>public.categories</c> and seeds the 12 shared
/// system categories (T1.3.1). User categories carry a nullable FK
/// <c>user_id → auth.users.id</c>, added in the migration via raw SQL.
/// </summary>
internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Icon).IsRequired().HasMaxLength(64);
        builder.Property(c => c.Color).IsRequired().HasMaxLength(7);
        builder.Property(c => c.IsSystem).IsRequired();

        // Fast lookup of a user's own categories (system rows have null user_id).
        builder.HasIndex(c => c.UserId);

        builder.HasData(SystemCategories);
    }

    private static IEnumerable<Category> SystemCategories =>
    [
        Category.CreateSystem(Id(0x01), "Продукты", "shopping-cart", "#22c55e"),
        Category.CreateSystem(Id(0x02), "Кафе и рестораны", "utensils", "#f97316"),
        Category.CreateSystem(Id(0x03), "Транспорт", "car", "#3b82f6"),
        Category.CreateSystem(Id(0x04), "Жильё", "house", "#8b5cf6"),
        Category.CreateSystem(Id(0x05), "Здоровье", "heart-pulse", "#ef4444"),
        Category.CreateSystem(Id(0x06), "Развлечения", "gamepad-2", "#ec4899"),
        Category.CreateSystem(Id(0x07), "Одежда", "shirt", "#14b8a6"),
        Category.CreateSystem(Id(0x08), "Связь и интернет", "wifi", "#06b6d4"),
        Category.CreateSystem(Id(0x09), "Образование", "graduation-cap", "#eab308"),
        Category.CreateSystem(Id(0x0A), "Подарки", "gift", "#d946ef"),
        Category.CreateSystem(Id(0x0B), "Зарплата", "wallet", "#10b981"),
        Category.CreateSystem(Id(0x0C), "Прочее", "ellipsis", "#64748b"),
    ];

    private static Guid Id(byte n) =>
        new($"a1c00000-0000-7000-8000-0000000000{n:x2}");
}
