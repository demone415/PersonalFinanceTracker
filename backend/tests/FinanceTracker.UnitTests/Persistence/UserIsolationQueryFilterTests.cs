using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Common;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.UnitTests.Persistence;

/// <summary>
/// Verifies the data-isolation global query filter (T1.1.12 / ARCHITECTURE.md
/// §11.2) on the real production helper: regular users see only their own rows,
/// admins see everything, and the bypass is explicit via IgnoreQueryFilters.
/// </summary>
public class UserIsolationQueryFilterTests
{
    private sealed record StubCurrentUser(Guid? UserId, bool IsAdmin) : ICurrentUserService;

    private sealed class OwnedRow : IUserOwnedEntity
    {
        public Guid Id { get; init; }
        public Guid UserId { get; init; }
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options, ICurrentUserService currentUser)
        : DbContext(options), IUserIsolatedContext
    {
        public ICurrentUserService CurrentUser => currentUser;

        public DbSet<OwnedRow> Rows => Set<OwnedRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OwnedRow>();
            UserIsolationQueryFilter.Apply(modelBuilder, this);
        }
    }

    private static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    private static TestDbContext NewContext(ICurrentUserService user, string dbName)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new TestDbContext(options, user);
    }

    private static void Seed(string dbName)
    {
        // Seed with an admin so all rows are inserted unfiltered.
        using var ctx = NewContext(new StubCurrentUser(null, IsAdmin: true), dbName);
        ctx.Rows.AddRange(
            new OwnedRow { Id = Guid.NewGuid(), UserId = Alice },
            new OwnedRow { Id = Guid.NewGuid(), UserId = Alice },
            new OwnedRow { Id = Guid.NewGuid(), UserId = Bob });
        ctx.SaveChanges();
    }

    [Fact]
    public void RegularUser_SeesOnlyOwnRows()
    {
        const string db = nameof(RegularUser_SeesOnlyOwnRows);
        Seed(db);

        using var ctx = NewContext(new StubCurrentUser(Alice, IsAdmin: false), db);
        var rows = ctx.Rows.ToList();

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(Alice, r.UserId));
    }

    [Fact]
    public void Admin_SeesAllRows()
    {
        const string db = nameof(Admin_SeesAllRows);
        Seed(db);

        using var ctx = NewContext(new StubCurrentUser(null, IsAdmin: true), db);

        Assert.Equal(3, ctx.Rows.Count());
    }

    [Fact]
    public void UnauthenticatedCaller_SeesNothing()
    {
        const string db = nameof(UnauthenticatedCaller_SeesNothing);
        Seed(db);

        using var ctx = NewContext(new StubCurrentUser(null, IsAdmin: false), db);

        Assert.Empty(ctx.Rows);
    }

    [Fact]
    public void IgnoreQueryFilters_BypassesIsolation()
    {
        const string db = nameof(IgnoreQueryFilters_BypassesIsolation);
        Seed(db);

        using var ctx = NewContext(new StubCurrentUser(Alice, IsAdmin: false), db);

        Assert.Equal(3, ctx.Rows.IgnoreQueryFilters().Count());
    }
}
