using FluentValidation;

namespace Academic.Application.Students.MarkOverdue;

/// <summary>
/// Validator for <see cref="MarkStudentOverdueCommand"/>. Runs in the MediatR pipeline
/// (ValidationBehavior) before the handler. StudentId must be a 26-char ULID.
/// </summary>
public sealed class MarkStudentOverdueCommandValidator : AbstractValidator<MarkStudentOverdueCommand>
{
    public MarkStudentOverdueCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty()
            .Length(26)
            .WithMessage("StudentId must be a 26-character ULID.");
    }
}
