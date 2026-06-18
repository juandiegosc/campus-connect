using BuildingBlocks.Application.Messaging;
using Identity.Application.Users.Login;

namespace Identity.Application.Users.Refresh;

/// <summary>
/// Command to rotate a refresh token — revokes the presented token and issues a new pair.
/// MUST implement <c>ICommand&lt;LoginResponse&gt;</c> (not IRequest) so that
/// UnitOfWorkBehavior activates and commits the revoke + INSERT atomically in one TX (ADR-026, Gotcha 16).
/// </summary>
/// <param name="RefreshToken">The opaque refresh token to rotate.</param>
public sealed record RefreshTokenCommand(
    string RefreshToken) : ICommand<LoginResponse>;
