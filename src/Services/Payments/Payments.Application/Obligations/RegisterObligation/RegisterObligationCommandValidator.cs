using FluentValidation;

namespace Payments.Application.Obligations.RegisterObligation;

public sealed class RegisterObligationCommandValidator : AbstractValidator<RegisterObligationCommand>
{
    public RegisterObligationCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("StudentId is required.")
            .Length(26).WithMessage("StudentId must be a 26-character ULID string.");

        RuleFor(x => x.Concept)
            .NotEmpty().WithMessage("Concept is required.")
            .MaximumLength(200).WithMessage("Concept must not exceed 200 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.DueDate)
            .NotEqual(default(DateTime)).WithMessage("DueDate must be provided.");
    }
}
