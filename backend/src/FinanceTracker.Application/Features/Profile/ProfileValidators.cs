using FluentValidation;

namespace FinanceTracker.Application.Features.Profile;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3).WithMessage("Currency must be a 3-letter ISO 4217 code.")
            .Matches("^[A-Za-z]{3}$").WithMessage("Currency must be three letters.");
        RuleFor(x => x.DisplayName).MaximumLength(100).When(x => x.DisplayName is not null);
    }
}
