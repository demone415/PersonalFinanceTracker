using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// Runs EF migrations and seeds initial data (GoTrue users + app records)
/// on first startup. Idempotent: no-ops if user profiles already exist.
/// </summary>
public static class DatabaseSeeder
{
    // ── Category IDs (must match CategoryConfiguration.cs) ──────────────────
    private static readonly Guid CatGroceries     = new("a1c00000-0000-7000-8000-000000000001");
    private static readonly Guid CatRestaurants   = new("a1c00000-0000-7000-8000-000000000002");
    private static readonly Guid CatTransport     = new("a1c00000-0000-7000-8000-000000000003");
    private static readonly Guid CatHousing       = new("a1c00000-0000-7000-8000-000000000004");
    private static readonly Guid CatHealth        = new("a1c00000-0000-7000-8000-000000000005");
    private static readonly Guid CatEntertainment = new("a1c00000-0000-7000-8000-000000000006");
    private static readonly Guid CatClothing      = new("a1c00000-0000-7000-8000-000000000007");
    private static readonly Guid CatComms         = new("a1c00000-0000-7000-8000-000000000008");
    private static readonly Guid CatSalary        = new("a1c00000-0000-7000-8000-00000000000b");
    private static readonly Guid CatOther         = new("a1c00000-0000-7000-8000-00000000000c");

    public static async Task SeedAsync(
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        // Build a seeder-only context: no ChangeLog interceptor, admin bypass on
        // query filters, UserId=null so ChangeLogInterceptor skips gracefully.
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        await using var ctx = new AppDbContext(opts, SeedUser.Instance);

        // 1. Apply pending EF migrations
        logger.LogInformation("[Seeder] Applying EF Core migrations…");
        await ctx.Database.MigrateAsync(cancellationToken);

        // 2. Idempotency guard
        if (await ctx.UserProfiles.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            logger.LogInformation("[Seeder] Already seeded — skipping.");
            return;
        }

        // 3. Create GoTrue seed users and capture their auto-generated IDs
        var adminApiUrl = (configuration["Supabase:AdminApiUrl"] ?? "http://localhost:9999").TrimEnd('/');
        var jwtSecret   = configuration["Supabase:JwtSecret"]
                          ?? "super-secret-jwt-token-with-at-least-32-characters-long";

        logger.LogInformation("[Seeder] Creating GoTrue users at {Url}…", adminApiUrl);

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", BuildServiceRoleJwt(jwtSecret));

        var user1Id = await CreateOrFindAsync(http, adminApiUrl, "user@example.com",   "Password123!", "user",  cancellationToken);
        var user2Id = await CreateOrFindAsync(http, adminApiUrl, "family@example.com", "Password123!", "user",  cancellationToken);
        var adminId = await CreateOrFindAsync(http, adminApiUrl, "admin@example.com",  "Password123!", "admin", cancellationToken);

        logger.LogInformation("[Seeder] Users: user1={U1} user2={U2} admin={A}", user1Id, user2Id, adminId);

        // 4. User profiles
        ctx.UserProfiles.AddRange(
            new UserProfile(user1Id, "Иван Петров",   "RUB"),
            new UserProfile(user2Id, "Анна Петрова",  "RUB"),
            new UserProfile(adminId, "Администратор", "RUB"));

        // 5. Accruals — 200+ records across Jan–Jun 2026 for 2 regular users
        var seed = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        AddAccruals(ctx, user1Id,  85_000m, 35_000m, seed, new Random(42));
        AddAccruals(ctx, user2Id, 120_000m, 50_000m, seed, new Random(99));

        // 6. Monthly budgets — 3 for user1 in the current seed month (June 2026),
        // so the progress view has data on first launch.
        ctx.MonthlyBudgets.AddRange(
            new MonthlyBudget(user1Id, CatGroceries,   2026, 6, 30_000m),
            new MonthlyBudget(user1Id, CatRestaurants, 2026, 6, 10_000m),
            new MonthlyBudget(user1Id, CatTransport,   2026, 6,  8_000m));

        await ctx.SaveChangesAsync(cancellationToken);
        logger.LogInformation("[Seeder] Done.");
    }

    // ── Accrual generation ───────────────────────────────────────────────────

    // Generates ≥ 17 transactions per month × 6 months = ≥ 102 per user (≥ 204 total).
    private static void AddAccruals(
        AppDbContext ctx, Guid userId, decimal salary, decimal rent,
        DateTimeOffset seed, Random rng)
    {
        for (var m = 0; m < 6; m++)
        {
            var mo = seed.AddMonths(m);

            // Income
            ctx.Accruals.Add(MakeAccrual(userId, salary, mo.AddDays(4),
                AccrualType.Income, CatSalary, "Зарплата"));

            // Fixed monthly expenses
            ctx.Accruals.Add(MakeAccrual(userId, rent, mo,
                AccrualType.Expense, CatHousing, "Аренда квартиры"));
            ctx.Accruals.Add(MakeAccrual(userId, Rnd(rng, 700, 1_200), mo.AddDays(9),
                AccrualType.Expense, CatComms, "Мобильная связь и интернет"));

            // Weekly groceries (4 trips)
            for (var w = 0; w < 4; w++)
                ctx.Accruals.Add(MakeAccrual(userId, Rnd(rng, 2_500, 7_000),
                    mo.AddDays(w * 7 + rng.Next(0, 3)),
                    AccrualType.Expense, CatGroceries, Pick(rng, Groceries)));

            // Restaurants (2–3)
            for (var r = 0; r < rng.Next(2, 4); r++)
                ctx.Accruals.Add(MakeAccrual(userId, Rnd(rng, 1_200, 4_500),
                    mo.AddDays(rng.Next(0, 28)),
                    AccrualType.Expense, CatRestaurants, Pick(rng, Restaurants)));

            // Transport (4 trips — one per week)
            for (var w = 0; w < 4; w++)
                ctx.Accruals.Add(MakeAccrual(userId, Rnd(rng, 400, 2_000),
                    mo.AddDays(w * 7 + rng.Next(0, 7)),
                    AccrualType.Expense, CatTransport, Pick(rng, Transport)));

            // Health (1–2 visits)
            for (var h = 0; h < rng.Next(1, 3); h++)
                ctx.Accruals.Add(MakeAccrual(userId, Rnd(rng, 1_000, 6_000),
                    mo.AddDays(rng.Next(0, 28)),
                    AccrualType.Expense, CatHealth, Pick(rng, Health)));

            // Entertainment (1–3 events)
            for (var e = 0; e < rng.Next(1, 4); e++)
                ctx.Accruals.Add(MakeAccrual(userId, Rnd(rng, 500, 3_000),
                    mo.AddDays(rng.Next(0, 28)),
                    AccrualType.Expense, CatEntertainment, Pick(rng, Entertainment)));

            // Clothing (always 1, occasionally 2)
            for (var c = 0; c < rng.Next(1, 3); c++)
                ctx.Accruals.Add(MakeAccrual(userId, Rnd(rng, 3_000, 15_000),
                    mo.AddDays(rng.Next(0, 28)),
                    AccrualType.Expense, CatClothing, Pick(rng, Clothing)));

            // Misc (always 1)
            ctx.Accruals.Add(MakeAccrual(userId, Rnd(rng, 200, 2_000),
                mo.AddDays(rng.Next(0, 28)),
                AccrualType.Expense, CatOther, "Разные расходы"));
        }
    }

    private static Domain.Entities.Accrual MakeAccrual(
        Guid userId, decimal amount, DateTimeOffset date,
        AccrualType type, Guid? catId, string desc) =>
        new(userId, amount, date, type, categoryId: catId, description: desc);

    private static decimal Rnd(Random rng, decimal min, decimal max) =>
        Math.Round(min + (max - min) * (decimal)rng.NextDouble(), 2);

    private static T Pick<T>(Random rng, T[] arr) => arr[rng.Next(arr.Length)];

    private static readonly string[] Groceries    = ["Магнит", "Перекрёсток", "ВкусВилл", "Пятёрочка", "Лента", "Ашан", "Дикси"];
    private static readonly string[] Restaurants  = ["Кафе «Уют»", "Шаурмячная", "Суши-бар", "Пицца Хаус", "Burger King", "KFC", "Теремок"];
    private static readonly string[] Transport    = ["Яндекс.Такси", "Метро", "Каршеринг", "Автобус", "Ситимобил", "Электричка"];
    private static readonly string[] Health       = ["Аптека", "Клиника", "Стоматология", "Анализы", "Очки / линзы", "Массаж"];
    private static readonly string[] Entertainment= ["Кино", "Концерт", "Спортзал", "Steam", "Netflix", "Зоопарк", "Боулинг"];
    private static readonly string[] Clothing     = ["Zara", "H&M", "Lamoda", "Wildberries", "O'STIN", "Befree", "Gloria Jeans"];

    // ── GoTrue helpers ───────────────────────────────────────────────────────

    private static async Task<Guid> CreateOrFindAsync(
        HttpClient http, string adminApiUrl,
        string email, string password, string role, CancellationToken ct)
    {
        var resp = await http.PostAsJsonAsync($"{adminApiUrl}/admin/users", new
        {
            email,
            password,
            email_confirm = true,
            app_metadata  = new { role },
        }, ct);

        if (resp.IsSuccessStatusCode)
        {
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            return Guid.Parse(doc.RootElement.GetProperty("id").GetString()!);
        }

        // 422 = user already exists (e.g. second run before postgres volume was wiped)
        if (resp.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var id = await FindByEmailAsync(http, adminApiUrl, email, ct);
            return id ?? throw new InvalidOperationException(
                $"User '{email}' exists in GoTrue but could not be found in user list.");
        }

        var body = await resp.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException(
            $"GoTrue user creation failed ({resp.StatusCode}): {body}");
    }

    private static async Task<Guid?> FindByEmailAsync(
        HttpClient http, string adminApiUrl, string email, CancellationToken ct)
    {
        var resp = await http.GetAsync($"{adminApiUrl}/admin/users?per_page=1000", ct);
        if (!resp.IsSuccessStatusCode) return null;

        using var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        // GoTrue v2 wraps users in { "users": [...] }; some builds return an array.
        var arr = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("users", out var u) ? u : root;

        foreach (var user in arr.EnumerateArray())
        {
            if (user.TryGetProperty("email", out var e) && e.GetString() == email
                && user.TryGetProperty("id",    out var id))
                return Guid.Parse(id.GetString()!);
        }

        return null;
    }

    // Generates an HS256 JWT with role=service_role — no external library needed.
    private static string BuildServiceRoleJwt(string secret)
    {
        var header  = B64U("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var iat     = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var exp     = DateTimeOffset.UtcNow.AddYears(10).ToUnixTimeSeconds();
        var payload = B64U($"{{\"role\":\"service_role\",\"iss\":\"supabase\",\"iat\":{iat},\"exp\":{exp}}}");
        var input   = $"{header}.{payload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return $"{input}.{B64U(hmac.ComputeHash(Encoding.UTF8.GetBytes(input)))}";
    }

    private static string B64U(string s) => B64U(Encoding.UTF8.GetBytes(s));

    private static string B64U(byte[] b) =>
        Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // ── Minimal ICurrentUserService for seeding: admin, no HTTP context ──────
    private sealed class SeedUser : ICurrentUserService
    {
        public static readonly SeedUser Instance = new();
        public Guid? UserId => null;   // null → ChangeLogInterceptor skips
        public bool IsAdmin  => true;  // true → query filters are bypassed
    }
}
