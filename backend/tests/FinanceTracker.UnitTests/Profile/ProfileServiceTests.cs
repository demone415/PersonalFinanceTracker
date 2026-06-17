using FinanceTracker.Application.Common.Exceptions;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Profile;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.UnitTests.Profile;

/// <summary>
/// Verifies base-currency selection in the user profile (Epic 8, T8.1.2): the
/// profile is created lazily on first access, updates persist a canonical
/// (upper-cased) ISO code, and an unauthenticated caller is rejected.
/// </summary>
public class ProfileServiceTests
{
    private static readonly Guid User = Guid.Parse("00000000-0000-0000-0000-0000000000d1");

    private sealed record StubCurrentUser(Guid? UserId, bool IsAdmin) : ICurrentUserService;

    private sealed class NoopUnitOfWork(AppDbContext ctx) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            ctx.SaveChangesAsync(cancellationToken);
    }

    private static AppDbContext NewContext(ICurrentUserService user, string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options, user);

    private static ProfileService ServiceFor(AppDbContext ctx) =>
        new(ctx, new NoopUnitOfWork(ctx), ctx.CurrentUser);

    [Fact]
    public async Task GetCurrent_CreatesDefaultRubProfile_WhenMissing()
    {
        const string db = nameof(GetCurrent_CreatesDefaultRubProfile_WhenMissing);
        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);

        var profile = await ServiceFor(ctx).GetCurrentAsync();

        Assert.Equal(User, profile.Id);
        Assert.Equal("RUB", profile.Currency);
        Assert.Null(profile.DisplayName);
        // Persisted, not just returned.
        Assert.True(await ctx.UserProfiles.AnyAsync(p => p.Id == User));
    }

    [Fact]
    public async Task Update_ChangesBaseCurrency_AndUpperCasesCode()
    {
        const string db = nameof(Update_ChangesBaseCurrency_AndUpperCasesCode);
        using (var seed = NewContext(new StubCurrentUser(User, IsAdmin: false), db))
        {
            seed.UserProfiles.Add(new UserProfile(User, "Иван", "RUB"));
            await seed.SaveChangesAsync();
        }

        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);
        var updated = await ServiceFor(ctx).UpdateAsync(new UpdateProfileRequest("Иван Петров", "usd"));

        Assert.Equal("USD", updated.Currency);
        Assert.Equal("Иван Петров", updated.DisplayName);

        var persisted = await ctx.UserProfiles.SingleAsync(p => p.Id == User);
        Assert.Equal("USD", persisted.Currency);
    }

    [Fact]
    public async Task Update_CreatesProfile_WhenMissing()
    {
        const string db = nameof(Update_CreatesProfile_WhenMissing);
        using var ctx = NewContext(new StubCurrentUser(User, IsAdmin: false), db);

        var updated = await ServiceFor(ctx).UpdateAsync(new UpdateProfileRequest(null, "EUR"));

        Assert.Equal("EUR", updated.Currency);
        Assert.True(await ctx.UserProfiles.AnyAsync(p => p.Id == User));
    }

    [Fact]
    public async Task GetCurrent_Throws_WhenUnauthenticated()
    {
        const string db = nameof(GetCurrent_Throws_WhenUnauthenticated);
        using var ctx = NewContext(new StubCurrentUser(UserId: null, IsAdmin: false), db);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => ServiceFor(ctx).GetCurrentAsync());
    }
}
