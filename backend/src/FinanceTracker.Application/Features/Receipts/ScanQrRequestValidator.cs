using FluentValidation;

namespace FinanceTracker.Application.Features.Receipts;

/// <summary>
/// Structural QR validation before a scan is accepted and enqueued (T4.1.5): the
/// payload must parse into the required fiscal fields (<c>t,s,fn,i,fp,n</c>).
/// </summary>
public sealed class ScanQrRequestValidator : AbstractValidator<ScanQrRequest>
{
    public ScanQrRequestValidator()
    {
        RuleFor(x => x.QrRaw)
            .NotEmpty().WithMessage("QR payload is required.")
            .MaximumLength(512)
            .Must(qr => QrCodeParser.TryParse(qr, out _))
            .WithMessage("QR payload is not a valid fiscal receipt code (expected t=&s=&fn=&i=&fp=&n=).");
    }
}
