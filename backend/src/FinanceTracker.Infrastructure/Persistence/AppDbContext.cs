using FinanceTracker.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the application's <c>public.*</c> schema.
/// The <c>auth.*</c> schema is owned by Supabase GoTrue and is never touched here.
/// Entity configurations are discovered from this assembly via
/// <see cref="ModelBuilder.ApplyConfigurationsFromAssembly(System.Reflection.Assembly)"/>.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUser)
    : DbContext(options), IUserIsolatedContext
{
    /// <summary>The authenticated caller, referenced by the data-isolation query filter.</summary>
    public ICurrentUserService CurrentUser => currentUser;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Data isolation by default (ARCHITECTURE.md §11.2): every user-owned
        // entity is filtered to the current caller; admins bypass via IsAdmin.
        UserIsolationQueryFilter.Apply(modelBuilder, this);

        base.OnModelCreating(modelBuilder);
    }
}
