using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Receipts;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;

namespace FinanceTracker.IntegrationTests;

/// <summary>
/// EF Core + real PostgreSQL (Testcontainers), as required by CLAUDE.md. Exercises
/// the background fetch end to end against an actual database: real migrations
/// (incl. the new <c>QrRaw</c> column + index), the <c>IgnoreQueryFilters</c>
/// background read, the manual receipt mapping, and a committed state transition.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ReceiptFetchProcessorPostgresTests : IAsyncLifetime
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly DateTimeOffset Now = new(2026, 6, 16, 10, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private string? _startupError;

    public async Task InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();

            // Our migrations add a raw-SQL FK to the GoTrue-owned auth.users table,
            // which a bare Postgres image lacks — create a minimal stub so the
            // migration applies (we never insert into it here).
            await using var ctx = NewContext();
            await ctx.Database.ExecuteSqlRawAsync(
                "CREATE SCHEMA IF NOT EXISTS auth; " +
                "CREATE TABLE IF NOT EXISTS auth.users (id uuid PRIMARY KEY);");
            await ctx.Database.MigrateAsync();
        }
        catch (Exception ex) when (ex is not Xunit.SkipException)
        {
            // No Docker / image pull failure → skip rather than fail the suite.
            _startupError = ex.Message;
        }
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private sealed record BackgroundUser : ICurrentUserService
    {
        public Guid? UserId => null;   // no HTTP user in background work
        public bool IsAdmin => true;
    }

    private AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new AppDbContext(options, new BackgroundUser());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [SkippableFact]
    public async Task Processor_FetchesAndPersists_AgainstRealPostgres()
    {
        Skip.If(_startupError is not null, $"Postgres container unavailable: {_startupError}");

        const string qr = "t=20200924T1837&s=349.93&fn=9282440300682838&i=46534&fp=1273019065&n=1";

        Guid receiptId;
        await using (var seed = NewContext())
        {
            var receipt = Receipt.CreateForQrScan(UserId, 34_993, Now, qr);
            seed.Receipts.Add(receipt);
            await seed.SaveChangesAsync();
            receiptId = receipt.Id;
        }

        await using (var run = NewContext())
        {
            var provider = new Mock<IReceiptProvider>();
            provider.Setup(p => p.GetReceiptAsync(qr, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ReceiptFetchResult.Successful(
                    new ReceiptData("ООО Ромашка", "Москва", "7700000000", "Касса", 7, "42",
                        34_993, TaxationType.Usn, 46534, "fn", "fp",
                        [new ReceiptItemData("Хлеб", 35.50m, 2m, 71.00m)]),
                    "{\"json\":true}"));

            var limiter = new Mock<IReceiptRateLimiter>();
            limiter.Setup(r => r.TryAcquireAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(RateLimitDecision.Allowed);

            var processor = new ReceiptFetchProcessor(
                run, new UnitOfWork(run), provider.Object, limiter.Object,
                Mock.Of<IReceiptDeadLetterQueue>(), new FixedTimeProvider(Now),
                NullLogger<ReceiptFetchProcessor>.Instance);

            var result = await processor.ProcessAsync(receiptId);

            Assert.Equal(ReceiptFetchProcessingStatus.Completed, result.Status);
        }

        await using var verify = NewContext();
        var saved = await verify.Receipts
            .IgnoreQueryFilters()
            .Include(r => r.Items)
            .SingleAsync(r => r.Id == receiptId);

        Assert.Equal(ReceiptFetchStatus.Fetched, saved.FetchStatus);
        Assert.Equal("ООО Ромашка", saved.Organization);
        Assert.Equal(qr, saved.QrRaw);              // QrRaw column round-trips
        Assert.Equal(1, saved.FetchAttempts);
        Assert.Single(saved.Items);
        Assert.Equal("Хлеб", saved.Items.First().Name);
    }
}
