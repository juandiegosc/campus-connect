using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Payments.Tests.Helpers;

/// <summary>
/// Helper for generating signed JWTs in integration tests (ADR-037).
/// Uses the same constants as Payments.API/appsettings.json to simulate tokens
/// issued by Identity service without spinning up a real Identity host.
/// </summary>
public static class JwtTestHelper
{
    private const string SigningKey = "campus-connect-dev-placeholder-key-32b";
    private const string Issuer    = "campusconnect";
    private const string Audience  = "campusconnect-clients";

    /// <summary>
    /// Creates a signed JWT with the given role.
    /// Claims: sub, role, name, schoolId (same as Identity Phase 3 token shape).
    /// </summary>
    public static string CreateToken(string role, string userId = "test-user-id")
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub",      userId),
            new Claim("role",     role),
            new Claim("name",     "Test User"),
            new Claim("schoolId", "SCH-001")
        };

        var token = new JwtSecurityToken(
            issuer:             Issuer,
            audience:           Audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
