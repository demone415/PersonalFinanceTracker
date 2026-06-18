using FinanceTracker.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF Core migrations on startup. Demo/seed data is no longer
/// created here — it is seeded by the Docker-only <c>db-seed</c> service from
/// <c>backend/db/seed.sql</c>, keeping seed data out of the application code.
/// </summary>
public static class DatabaseMigrator
{
    public static async Task MigrateAsync(
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        // A migrator-only context: admin bypass on query filters, UserId=null so
        // the ChangeLogInterceptor skips gracefully.
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        await using var ctx = new AppDbContext(opts, MigratorUser.Instance);

        logger.LogInformation("[Migrator] Applying EF Core migrations…");
        await ctx.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("[Migrator] Migrations up to date.");
    }

    // Minimal ICurrentUserService for migrations: admin, no HTTP context.
    private sealed class MigratorUser : ICurrentUserService
    {
        public static readonly MigratorUser Instance = new();
        public Guid? UserId => null;   // null → ChangeLogInterceptor skips
        public bool IsAdmin => true;   // true → query filters are bypassed
    }
}
