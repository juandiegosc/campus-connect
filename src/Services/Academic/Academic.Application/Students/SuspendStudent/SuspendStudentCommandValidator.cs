using FluentValidation;

namespace Academic.Application.Students.SuspendStudent;

/// <summary>
/// Validator for <see cref="SuspendStudentCommand"/>. Runs in the MediatR pipeline
/// (ValidationBehavior) before the handler. StudentId must be a 26-char ULID.
/// </summary>
public sealed class SuspendStudentCommandValidator : AbstractValidator<SuspendStudentCommand>
{
    public SuspendStudentCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty()
            .Length(26)
            .WithMessage("StudentId must be a 26-character ULID.");
    }
}
