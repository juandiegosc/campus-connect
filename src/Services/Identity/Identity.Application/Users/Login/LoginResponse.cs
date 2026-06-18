namespace Identity.Application.Users.Login;

/// <summary>
/// DTO returned by <c>LoginCommand</c> and <c>RefreshTokenCommand</c> on success.
/// </summary>
/// <param name="AccessToken">Signed JWT access token.</param>
/// <param name="RefreshToken">Opaque single-use refresh token (plain Guid string, ADR-027).</param>
/// <param name="ExpiresAt">UTC expiry time of the <paramref name="AccessToken"/>.</param>
/// <param name="Role">User's role (string representation of <c>UserRole</c>).</param>
/// <param name="FullName">User's display name.</param>
public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string Role,
    string FullName);
