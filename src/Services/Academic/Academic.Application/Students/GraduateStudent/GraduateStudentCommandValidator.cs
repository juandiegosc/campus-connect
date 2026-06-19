using FluentValidation;

namespace Academic.Application.Students.GraduateStudent;

/// <summary>
/// Validator for <see cref="GraduateStudentCommand"/>. Runs in the MediatR pipeline
/// (ValidationBehavior) before the handler. StudentId must be a 26-char ULID.
/// </summary>
public sealed class GraduateStudentCommandValidator : AbstractValidator<GraduateStudentCommand>
{
    public GraduateStudentCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty()
            .Length(26)
            .WithMessage("StudentId must be a 26-character ULID.");
    }
}
