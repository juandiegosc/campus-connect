using BuildingBlocks.Application.Messaging;
using Identity.Domain.Users;

namespace Identity.Application.Users.RegisterUser;

/// <summary>
/// Command to register a new user in the Identity bounded context.
/// Handled by <see cref="RegisterUserCommandHandler"/>.
/// Validated by <see cref="RegisterUserCommandValidator"/> via ValidationBehavior.
/// </summary>
/// <param name="Username">Unique username (≤ 64 chars, alphanumeric + . _ -).</param>
/// <param name="FullName">Full display name of the user (≤ 200 chars).</param>
/// <param name="Password">Raw password (8–128 chars). Hashed by IPasswordHasher before persistence.</param>
/// <param name="Role">The role to assign to the new user.</param>
public sealed record RegisterUserCommand(
    string Username,
    string FullName,
    string Password,
    UserRole Role) : ICommand<Guid>;
