using FluentValidation;

namespace Identity.Application.Users.RegisterUser;

/// <summary>
/// FluentValidation validator for <see cref="RegisterUserCommand"/>.
/// Executed by the kernel's <c>ValidationBehavior</c> pipeline before the handler runs.
/// </summary>
internal sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MaximumLength(64).WithMessage("Username must not exceed 64 characters.")
            .Matches(@"^[a-zA-Z0-9._-]+$").WithMessage("Username may only contain letters, digits, dots, underscores, and hyphens.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("FullName is required.")
            .MaximumLength(200).WithMessage("FullName must not exceed 200 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128).WithMessage("Password must not exceed 128 characters.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Role must be a valid UserRole value.");
    }
}
