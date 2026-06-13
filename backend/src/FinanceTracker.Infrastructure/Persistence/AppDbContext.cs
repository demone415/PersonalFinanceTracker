using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the application's <c>public.*</c> schema.
/// The <c>auth.*</c> schema is owned by Supabase GoTrue and is never touched here.
/// Entity configurations are discovered from this assembly via
/// <see cref="ModelBuilder.ApplyConfigurationsFromAssembly(System.Reflection.Assembly)"/>.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
