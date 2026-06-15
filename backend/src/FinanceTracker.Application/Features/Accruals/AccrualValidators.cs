using FinanceTracker.Domain.Enums;
using FluentValidation;

namespace FinanceTracker.Application.Features.Accruals;

public sealed class CreateAccrualRequestValidator : AbstractValidator<CreateAccrualRequest>
{
    public CreateAccrualRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be greater than zero.");
        RuleFor(x => x.Date).NotEmpty().WithMessage("Date is required.");
        RuleFor(x => x.Type).IsInEnum().WithMessage("Invalid accrual type.");
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3).WithMessage("Currency must be a 3-letter ISO 4217 code.");
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
        RuleFor(x => x.Tags).Must(t => t == null || t.Count <= 20).WithMessage("Maximum 20 tags allowed.");
        RuleForEach(x => x.Tags).MaximumLength(64);
        RuleFor(x => x.ExchangeRate).GreaterThan(0).When(x => x.ExchangeRate is not null);
    }
}

public sealed class UpdateAccrualRequestValidator : AbstractValidator<UpdateAccrualRequest>
{
    public UpdateAccrualRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be greater than zero.");
        RuleFor(x => x.Date).NotEmpty().WithMessage("Date is required.");
        RuleFor(x => x.Type).IsInEnum().WithMessage("Invalid accrual type.");
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3).WithMessage("Currency must be a 3-letter ISO 4217 code.");
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
        RuleFor(x => x.Tags).Must(t => t == null || t.Count <= 20).WithMessage("Maximum 20 tags allowed.");
        RuleForEach(x => x.Tags).MaximumLength(64);
        RuleFor(x => x.ExchangeRate).GreaterThan(0).When(x => x.ExchangeRate is not null);
    }
}

public sealed class CreateReceiptItemRequestValidator : AbstractValidator<CreateReceiptItemRequest>
{
    public CreateReceiptItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Sum).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateReceiptItemRequestValidator : AbstractValidator<UpdateReceiptItemRequest>
{
    public UpdateReceiptItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Sum).GreaterThanOrEqualTo(0);
    }
}
