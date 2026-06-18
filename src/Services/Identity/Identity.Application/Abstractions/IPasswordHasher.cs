namespace Identity.Application.Abstractions;

/// <summary>
/// Port for password hashing. Implemented by <c>BcryptPasswordHasher</c> in
/// Identity.Infrastructure (ADR-017). No BCrypt types are exposed here.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes the raw password string and returns the resulting hash.
    /// </summary>
    /// <param name="raw">The plain-text password to hash.</param>
    /// <returns>A hashed representation of <paramref name="raw"/>.</returns>
    string Hash(string raw);

    /// <summary>
    /// Verifies whether <paramref name="raw"/> matches the provided <paramref name="hash"/>.
    /// </summary>
    /// <param name="raw">The plain-text password to verify.</param>
    /// <param name="hash">The stored hash to compare against.</param>
    /// <returns><c>true</c> if the password matches; otherwise <c>false</c>.</returns>
    bool Verify(string raw, string hash);
}
