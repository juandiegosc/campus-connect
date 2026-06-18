using Identity.Domain.Users;

namespace Identity.Application.Abstractions;

/// <summary>
/// Port for JWT access token and opaque refresh token generation.
/// Implemented by <c>JwtTokenService</c> in Identity.Infrastructure.Security.
/// No System.IdentityModel or cryptography types appear in this interface (ADR-025).
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Creates a signed JWT access token for the given user.
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <returns>An <see cref="AccessTokenResult"/> containing the token string and its UTC expiry.</returns>
    AccessTokenResult CreateAccessToken(User user);

    /// <summary>
    /// Creates a new opaque refresh token (Guid string).
    /// </summary>
    /// <returns>A non-empty, URL-safe string suitable for single-use refresh rotation (ADR-026).</returns>
    string CreateRefreshToken();

    /// <summary>
    /// The lifetime of refresh tokens. Used by handlers to compute <c>ExpiresAt = now + RefreshTokenLifetime</c>.
    /// </summary>
    TimeSpan RefreshTokenLifetime { get; }
}

/// <summary>
/// Result of <see cref="IJwtTokenService.CreateAccessToken"/>.
/// </summary>
/// <param name="Token">The signed JWT string.</param>
/// <param name="ExpiresAtUtc">UTC expiry time (used in login/refresh responses).</param>
public sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc);
