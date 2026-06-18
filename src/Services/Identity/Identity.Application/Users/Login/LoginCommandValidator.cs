using FluentValidation;

namespace Identity.Application.Users.Login;

/// <summary>
/// FluentValidation validator for <see cref="LoginCommand"/>.
/// Runs in the ValidationBehavior kernel pipeline before the handler.
/// </summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MaximumLength(64).WithMessage("Username must not exceed 64 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(128).WithMessage("Password must not exceed 128 characters.");
    }
}
