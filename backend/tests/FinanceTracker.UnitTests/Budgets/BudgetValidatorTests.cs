using FinanceTracker.Application.Features.Budgets;

namespace FinanceTracker.UnitTests.Budgets;

/// <summary>
/// Validation rules for the budget payloads (T5.1.2): a positive limit, a
/// 3-letter currency code, a sane year and a 1–12 month are required.
/// </summary>
public class BudgetValidatorTests
{
    private static readonly Guid Category = Guid.Parse("a1c00000-0000-7000-8000-000000000001");
    private readonly CreateBudgetRequestValidator _create = new();
    private readonly UpdateBudgetRequestValidator _update = new();

    private static CreateBudgetRequest Valid(decimal limit = 10_000m, string currency = "RUB",
        int year = 2026, int month = 6) =>
        new(Category, year, month, limit, currency);

    [Fact]
    public void Create_ValidRequest_Passes() =>
        Assert.True(_create.Validate(Valid()).IsValid);

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Create_NonPositiveLimit_Fails(decimal limit) =>
        Assert.False(_create.Validate(Valid(limit: limit)).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("RU")]
    [InlineData("ROUBLE")]
    public void Create_BadCurrency_Fails(string currency) =>
        Assert.False(_create.Validate(Valid(currency: currency)).IsValid);

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Create_MonthOutOfRange_Fails(int month) =>
        Assert.False(_create.Validate(Valid(month: month)).IsValid);

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    public void Create_YearOutOfRange_Fails(int year) =>
        Assert.False(_create.Validate(Valid(year: year)).IsValid);

    [Fact]
    public void Create_EmptyCategory_Fails() =>
        Assert.False(_create.Validate(new CreateBudgetRequest(Guid.Empty, 2026, 6, 10_000m, "RUB")).IsValid);

    [Fact]
    public void Update_ValidRequest_Passes() =>
        Assert.True(_update.Validate(new UpdateBudgetRequest(5_000m, "RUB")).IsValid);

    [Fact]
    public void Update_NonPositiveLimit_Fails() =>
        Assert.False(_update.Validate(new UpdateBudgetRequest(0m, "RUB")).IsValid);
}
