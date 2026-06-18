using Identity.Application.Abstractions;

namespace Identity.Infrastructure.Security;

/// <summary>
/// BCrypt implementation of <see cref="IPasswordHasher"/> (ADR-024).
/// Work factor 12 (~250–400ms on modern hardware in 2025–2026).
/// No BCrypt types are exposed in the public API — callers only see <see cref="IPasswordHasher"/>.
/// </summary>
internal sealed class BcryptPasswordHasher : IPasswordHasher
{
    /// <summary>BCrypt work factor. OWASP 2024 recommends ≥ 10; 12 is the chosen sweet spot.</summary>
    private const int WorkFactor = 12;

    /// <inheritdoc />
    public string Hash(string raw) => BCrypt.Net.BCrypt.HashPassword(raw, WorkFactor);

    /// <inheritdoc />
    public bool Verify(string raw, string hash) => BCrypt.Net.BCrypt.Verify(raw, hash);
}
