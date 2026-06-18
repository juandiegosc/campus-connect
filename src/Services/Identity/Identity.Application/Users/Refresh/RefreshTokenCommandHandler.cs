using BuildingBlocks.Application.Common;
using Identity.Application.Abstractions;
using Identity.Application.Users.Login;
using Identity.Domain.RefreshTokens;
using Identity.Domain.Users;
using MediatR;

namespace Identity.Application.Users.Refresh;

/// <summary>
/// Handles <see cref="RefreshTokenCommand"/>.
/// Flow (exact order per design §4.2):
///   1. FindByTokenAsync → null → Unauthorized
///   2. !rt.IsActive(now) → Unauthorized (covers revoked + expired with single message)
///   3. rt.Revoke() — EF tracking marks entity Modified
///   4. FindByIdAsync(rt.UserId) → null or !IsActive → Unauthorized
///   5. CreateAccessToken(user) → AccessTokenResult
///   6. CreateRefreshToken() → new string
///   7. RefreshToken.Issue(userId, newToken, expiry, now)
///   8. AddAsync(newRt) — staged for INSERT
///   9. Return Success(LoginResponse)
///   UnitOfWorkBehavior commits UPDATE (revoke) + INSERT (new token) atomically (ADR-026).
/// </summary>
public sealed class RefreshTokenCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IUserRepository userRepository,
    IJwtTokenService jwtTokenService,
    TimeProvider timeProvider)
    : IRequestHandler<RefreshTokenCommand, Result<LoginResponse>>
{
    private static readonly Error InvalidRefresh =
        Error.Unauthorized("identity.auth.invalid_refresh", "Invalid or expired refresh token.");

    public async Task<Result<LoginResponse>> Handle(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // 1. Look up the presented refresh token.
        var rt = await refreshTokenRepository.FindByTokenAsync(command.RefreshToken, cancellationToken);
        if (rt is null)
            return Result<LoginResponse>.Failure(InvalidRefresh);

        // 2. Validate it is still active (not revoked AND not expired).
        if (!rt.IsActive(now))
            return Result<LoginResponse>.Failure(InvalidRefresh);

        // 3. Revoke the old token — EF change tracking picks up IsRevoked = true.
        rt.Revoke();

        // 4. Load the user — defensive: user may have been deactivated after initial login.
        var user = await userRepository.FindByIdAsync(UserId.From(rt.UserId), cancellationToken);
        if (user is null || !user.IsActive)
            return Result<LoginResponse>.Failure(InvalidRefresh);

        // 5. Issue new access token.
        var accessToken = jwtTokenService.CreateAccessToken(user);

        // 6. Issue new refresh token string.
        var newRefreshTokenStr = jwtTokenService.CreateRefreshToken();

        // 7. Create the new RefreshToken domain entity.
        var newRefreshToken = RefreshToken.Issue(
            user.Id.Value,
            newRefreshTokenStr,
            now.Add(jwtTokenService.RefreshTokenLifetime),
            now);

        // 8. Stage for INSERT — UnitOfWorkBehavior commits revoke + insert atomically.
        await refreshTokenRepository.AddAsync(newRefreshToken, cancellationToken);

        // 9. Return success with the new token pair.
        return Result<LoginResponse>.Success(new LoginResponse(
            accessToken.Token,
            newRefreshTokenStr,
            accessToken.ExpiresAtUtc,
            user.Role.ToString(),
            user.FullName));
    }
}
