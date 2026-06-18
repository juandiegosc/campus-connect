using FluentValidation;
using Payments.Domain.Obligations;

namespace Payments.Application.Obligations.ConfirmPayment;

public sealed class ConfirmPaymentCommandValidator : AbstractValidator<ConfirmPaymentCommand>
{
    public ConfirmPaymentCommandValidator()
    {
        RuleFor(x => x.ObligationId)
            .NotEmpty().WithMessage("ObligationId is required.")
            .Length(26).WithMessage("ObligationId must be a 26-character ULID string.");

        RuleFor(x => x.Method)
            .NotEmpty().WithMessage("Method is required.")
            .Must(m => Enum.TryParse<PaymentMethod>(m, ignoreCase: true, out _))
            .WithMessage($"Method must be one of: {string.Join(", ", Enum.GetNames<PaymentMethod>())}.");

        RuleFor(x => x.Reference)
            .NotEmpty().WithMessage("Reference is required.")
            .MaximumLength(100).WithMessage("Reference must not exceed 100 characters.");
    }
}
