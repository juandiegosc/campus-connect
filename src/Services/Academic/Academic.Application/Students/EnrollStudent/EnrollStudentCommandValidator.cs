using FluentValidation;

namespace Academic.Application.Students.EnrollStudent;

/// <summary>
/// FluentValidation validator for <see cref="EnrollStudentCommand"/>.
/// Runs in the MediatR pipeline BEFORE the handler (ValidationBehavior).
/// </summary>
public sealed class EnrollStudentCommandValidator : AbstractValidator<EnrollStudentCommand>
{
    public EnrollStudentCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.DocumentId)
            .NotEmpty()
            .Matches(@"^[A-Za-z0-9]{6,15}$")
            .WithMessage("DocumentId must be 6-15 alphanumeric characters.");

        RuleFor(x => x.Grade)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(x => x.GuardianName)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.GuardianEmail)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("GuardianEmail must be a valid email address.");
    }
}
