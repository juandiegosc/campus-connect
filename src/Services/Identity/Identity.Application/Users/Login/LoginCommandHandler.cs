using BuildingBlocks.Application.Common;
using Identity.Application.Abstractions;
using Identity.Domain.RefreshTokens;
using MediatR;

namespace Identity.Application.Users.Login;

/// <summary>
/// Handles <see cref="LoginCommand"/>.
/// Flow (exact order per design §4.1):
///   1. FindByUsernameAsync → null → Unauthorized (no enumeration)
///   2. Verify password → false → same Unauthorized
///   3. IsActive → false → same Unauthorized (checked AFTER Verify to prevent timing oracle)
///   4. CreateAccessToken → AccessTokenResult
///   5. CreateRefreshToken → string
///   6. RefreshToken.Issue(userId, token, expiry, now)
///   7. IRefreshTokenRepository.AddAsync(rt)
///   8. Return Success(LoginResponse)
///   UnitOfWorkBehavior commits the INSERT atomically after this returns.
/// </summary>
public sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IRefreshTokenRepository refreshTokenRepository,
    TimeProvider timeProvider)
    : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private static readonly Error InvalidCredentials =
        Error.Unauthorized("identity.auth.invalid_credentials", "Invalid username or password.");

    public async Task<Result<LoginResponse>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // 1. Look up user by username — null means user doesn't exist.
        var user = await userRepository.FindByUsernameAsync(command.Username, cancellationToken);
        if (user is null)
            return Result<LoginResponse>.Failure(InvalidCredentials);

        // 2. Verify password BEFORE checking IsActive to prevent timing oracle
        //    (Attacker cannot distinguish "wrong password" from "valid user, wrong password").
        var passwordValid = passwordHasher.Verify(command.Password, user.PasswordHash.Value);
        if (!passwordValid)
            return Result<LoginResponse>.Failure(InvalidCredentials);

        // 3. Check active status AFTER password verification.
        if (!user.IsActive)
            return Result<LoginResponse>.Failure(InvalidCredentials);

        // 4. Issue access token.
        var accessToken = jwtTokenService.CreateAccessToken(user);

        // 5. Issue opaque refresh token string.
        var refreshTokenStr = jwtTokenService.CreateRefreshToken();

        // 6. Create the RefreshToken domain entity.
        var refreshToken = RefreshToken.Issue(
            user.Id.Value,
            refreshTokenStr,
            now.Add(jwtTokenService.RefreshTokenLifetime),
            now);

        // 7. Stage for persistence — UnitOfWorkBehavior commits after this handler returns.
        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        // 8. Return success.
        return Result<LoginResponse>.Success(new LoginResponse(
            accessToken.Token,
            refreshTokenStr,
            accessToken.ExpiresAtUtc,
            user.Role.ToString(),
            user.FullName));
    }
}
