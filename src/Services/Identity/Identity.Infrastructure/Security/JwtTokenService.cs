using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Identity.Application.Abstractions;
using Identity.Domain.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Security;

/// <summary>
/// Implements <see cref="IJwtTokenService"/> using HMAC-SHA256 signed JWTs.
/// Reads configuration from <c>Jwt:*</c> section (ADR-025, ADR-030).
/// Defaults match the Gateway contract — MUST remain identical (R1).
/// </summary>
internal sealed class JwtTokenService : IJwtTokenService
{
    private readonly TimeProvider _time;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly int _accessTokenMinutes;

    /// <inheritdoc />
    public TimeSpan RefreshTokenLifetime { get; }

    public JwtTokenService(IConfiguration configuration, TimeProvider time)
    {
        _time = time;

        var jwt = configuration.GetSection("Jwt");

        _issuer = jwt["Issuer"] ?? "campusconnect";
        _audience = jwt["Audience"] ?? "campusconnect-clients";

        var signingKeyValue = jwt["SigningKey"];
        // Fall back to dev placeholder when env var Jwt__SigningKey is not set (local-only project).
        var signingKey = string.IsNullOrWhiteSpace(signingKeyValue)
            ? "campus-connect-dev-placeholder-key-32b"
            : signingKeyValue;

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        _accessTokenMinutes = int.TryParse(jwt["AccessTokenMinutes"], out var m) ? m : 60;

        var days = int.TryParse(jwt["RefreshTokenDays"], out var d) ? d : 7;
        RefreshTokenLifetime = TimeSpan.FromDays(days);
    }

    /// <inheritdoc />
    public AccessTokenResult CreateAccessToken(User user)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var exp = now.AddMinutes(_accessTokenMinutes);

        var claims = new List<Claim>
        {
            new("sub",          user.Id.Value.ToString()),
            new("unique_name",  user.Username),
            new("name",         user.FullName),
            new("role",         user.Role.ToString()),
            new("schoolId",     "SCH-001"),               // ADR-030 — TODO multi-tenant: derive from User.SchoolId when multi-tenancy is introduced
            new("jti",          Guid.NewGuid().ToString()),
            new("iat",          new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                                ClaimValueTypes.Integer64)
        };

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now,
            expires: exp,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessTokenResult(tokenString, exp);
    }

    /// <inheritdoc />
    public string CreateRefreshToken() => Guid.NewGuid().ToString();
}
