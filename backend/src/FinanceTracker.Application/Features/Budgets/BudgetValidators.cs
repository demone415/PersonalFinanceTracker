using FluentValidation;

namespace FinanceTracker.Application.Features.Budgets;

/// <summary>Shared rules for the create/update budget payloads.</summary>
internal static class BudgetRules
{
    public static IRuleBuilderOptions<T, decimal> BudgetLimit<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThan(0).WithMessage("Limit must be greater than zero.");

    public static IRuleBuilderOptions<T, string> BudgetCurrency<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Length(3).WithMessage("Currency must be a 3-letter code, e.g. RUB.");

    public static IRuleBuilderOptions<T, int> BudgetYear<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(2000, 2100);

    public static IRuleBuilderOptions<T, int> BudgetMonth<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(1, 12);
}

public sealed class CreateBudgetRequestValidator : AbstractValidator<CreateBudgetRequest>
{
    public CreateBudgetRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Year).BudgetYear();
        RuleFor(x => x.Month).BudgetMonth();
        RuleFor(x => x.LimitAmount).BudgetLimit();
        RuleFor(x => x.Currency).BudgetCurrency();
    }
}

public sealed class UpdateBudgetRequestValidator : AbstractValidator<UpdateBudgetRequest>
{
    public UpdateBudgetRequestValidator()
    {
        RuleFor(x => x.LimitAmount).BudgetLimit();
        RuleFor(x => x.Currency).BudgetCurrency();
    }
}
