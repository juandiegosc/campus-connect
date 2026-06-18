using BuildingBlocks.Domain.Exceptions;

namespace Identity.Domain.RefreshTokens;

/// <summary>
/// Satellite entity for refresh token management.
/// NOT an aggregate root — does NOT raise domain events.
/// Factory: <see cref="Issue"/>. Terminal operation: <see cref="Revoke"/>.
/// </summary>
public sealed class RefreshToken
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>Opaque token value (Guid string, max 128 chars).</summary>
    public string Token { get; private set; } = default!;

    /// <summary>FK to User.Id.Value — plain Guid to keep this entity decoupled from UserId VO.</summary>
    public Guid UserId { get; private set; }

    /// <summary>UTC expiry timestamp.</summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>Whether this token has been revoked (single-use rotation — ADR-026).</summary>
    public bool IsRevoked { get; private set; }

    /// <summary>UTC creation timestamp (set by factory from nowUtc for testability).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Private parameterless constructor required by EF Core for materialisation.</summary>
    private RefreshToken() { }

    /// <summary>
    /// Creates a new <see cref="RefreshToken"/> with the given parameters, enforcing domain guards.
    /// </summary>
    /// <param name="userId">The user this token belongs to. Must not be <see cref="Guid.Empty"/>.</param>
    /// <param name="token">The opaque token string. Must not be null or whitespace.</param>
    /// <param name="expiresAtUtc">The UTC expiry. Must be strictly after <paramref name="nowUtc"/>.</param>
    /// <param name="nowUtc">Current UTC time (injected for testability).</param>
    /// <returns>A new, non-revoked <see cref="RefreshToken"/>.</returns>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="userId"/> is empty, <paramref name="token"/> is blank,
    /// or <paramref name="expiresAtUtc"/> is not in the future relative to <paramref name="nowUtc"/>.
    /// </exception>
    public static RefreshToken Issue(Guid userId, string token, DateTime expiresAtUtc, DateTime nowUtc)
    {
        if (userId == Guid.Empty)
            throw new DomainException("UserId cannot be empty.");

        if (string.IsNullOrWhiteSpace(token))
            throw new DomainException("Token cannot be null or whitespace.");

        if (expiresAtUtc <= nowUtc)
            throw new DomainException("ExpiresAt must be in the future relative to nowUtc.");

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = token,
            UserId = userId,
            ExpiresAt = expiresAtUtc,
            IsRevoked = false,
            CreatedAt = nowUtc
        };
    }

    /// <summary>
    /// Revokes this token. Idempotency is deliberately NOT supported — double-revoke
    /// indicates a replay attempt and should be surfaced as a domain exception.
    /// </summary>
    /// <exception cref="DomainException">Thrown if the token is already revoked.</exception>
    public void Revoke()
    {
        if (IsRevoked)
            throw new DomainException("Token is already revoked.");

        IsRevoked = true;
    }

    /// <summary>
    /// Returns <c>true</c> when the token can still be used:
    /// not revoked AND <see cref="ExpiresAt"/> is strictly after <paramref name="nowUtc"/>.
    /// </summary>
    public bool IsActive(DateTime nowUtc) => !IsRevoked && ExpiresAt > nowUtc;
}
