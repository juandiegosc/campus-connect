using FluentValidation;

namespace Academic.Application.Students.ReactivateStudent;

/// <summary>
/// Validator for <see cref="ReactivateStudentCommand"/>. Runs in the MediatR pipeline
/// (ValidationBehavior) before the handler. StudentId must be a 26-char ULID.
/// </summary>
public sealed class ReactivateStudentCommandValidator : AbstractValidator<ReactivateStudentCommand>
{
    public ReactivateStudentCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty()
            .Length(26)
            .WithMessage("StudentId must be a 26-character ULID.");
    }
}
