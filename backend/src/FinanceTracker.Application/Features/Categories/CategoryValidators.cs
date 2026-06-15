using FluentValidation;

namespace FinanceTracker.Application.Features.Categories;

/// <summary>Shared rules for the create/update category payloads.</summary>
internal static class CategoryRules
{
    // 6-digit hex colour, e.g. #22c55e.
    public const string HexColorPattern = "^#([0-9A-Fa-f]{6})$";

    public static IRuleBuilderOptions<T, string> CategoryName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(100);

    public static IRuleBuilderOptions<T, string> CategoryIcon<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(64);

    public static IRuleBuilderOptions<T, string> CategoryColor<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Matches(HexColorPattern).WithMessage("Color must be a 6-digit HEX value, e.g. #22c55e.");
}

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).CategoryName();
        RuleFor(x => x.Icon).CategoryIcon();
        RuleFor(x => x.Color).CategoryColor();
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).CategoryName();
        RuleFor(x => x.Icon).CategoryIcon();
        RuleFor(x => x.Color).CategoryColor();
    }
}
