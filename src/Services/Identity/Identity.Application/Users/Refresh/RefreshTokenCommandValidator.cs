using FluentValidation;

namespace Identity.Application.Users.Refresh;

/// <summary>
/// FluentValidation validator for <see cref="RefreshTokenCommand"/>.
/// Runs in the ValidationBehavior kernel pipeline before the handler.
/// </summary>
public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("RefreshToken is required.")
            .MaximumLength(128).WithMessage("RefreshToken must not exceed 128 characters.");
    }
}
