using BuildingBlocks.Application.Messaging;

namespace Identity.Application.Users.Login;

/// <summary>
/// Command to authenticate a user and issue a JWT access token + refresh token pair.
/// MUST implement <c>ICommand&lt;LoginResponse&gt;</c> (not IRequest) so that
/// UnitOfWorkBehavior activates and commits the refresh token INSERT atomically (Gotcha 16, R2).
/// </summary>
/// <param name="Username">The user's username.</param>
/// <param name="Password">The raw (unhashed) password.</param>
public sealed record LoginCommand(
    string Username,
    string Password) : ICommand<LoginResponse>;
