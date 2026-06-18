using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Primitives;
using Identity.Domain.Users.Events;

namespace Identity.Domain.Users;

/// <summary>
/// Aggregate root for the Identity bounded context.
/// Encapsulates user invariants: non-empty username (≤64 chars), non-empty full name (≤200 chars),
/// a non-null <see cref="PasswordHash"/> value object, and a valid <see cref="UserRole"/>.
/// A successful <see cref="Create"/> call raises exactly one <see cref="UserCreatedDomainEvent"/>.
/// </summary>
public sealed class User : AggregateRoot<UserId>
{
    /// <summary>Unique username (≤ 64 characters).</summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>Display name of the user (≤ 200 characters).</summary>
    public string FullName { get; private set; } = string.Empty;

    /// <summary>Hashed password value object. Never null after creation.</summary>
    public PasswordHash PasswordHash { get; private set; } = null!;

    /// <summary>Role assigned to this user.</summary>
    public UserRole Role { get; private set; }

    /// <summary>Whether the user account is active.</summary>
    public bool IsActive { get; private set; }

    /// <summary>UTC timestamp of account creation.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Parameterless constructor required by EF Core for materialisation.
    /// Do NOT use directly — use <see cref="Create"/> instead.
    /// </summary>
    private User() { }

    /// <summary>
    /// Creates a new <see cref="User"/> aggregate enforcing all domain invariants.
    /// Raises exactly one <see cref="UserCreatedDomainEvent"/> on success.
    /// </summary>
    /// <param name="id">Pre-generated <see cref="UserId"/>.</param>
    /// <param name="username">Non-null, non-empty username (max 64 chars).</param>
    /// <param name="fullName">Non-null, non-empty full name (max 200 chars).</param>
    /// <param name="passwordHash">Pre-computed <see cref="PasswordHash"/> value object.</param>
    /// <param name="role">A valid <see cref="UserRole"/> enum value.</param>
    /// <param name="nowUtc">Current UTC time, provided by <c>TimeProvider</c>.</param>
    /// <returns>A new <see cref="User"/> aggregate with one pending domain event.</returns>
    /// <exception cref="DomainException">
    /// Thrown when any invariant is violated (empty username, username too long,
    /// empty full name, full name too long, or null password hash).
    /// </exception>
    public static User Create(
        UserId id,
        string username,
        string fullName,
        PasswordHash passwordHash,
        UserRole role,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new DomainException("Username cannot be null or empty.");

        if (username.Length > 64)
            throw new DomainException("Username cannot exceed 64 characters.");

        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("FullName cannot be null or empty.");

        if (fullName.Length > 200)
            throw new DomainException("FullName cannot exceed 200 characters.");

        if (passwordHash is null)
            throw new DomainException("PasswordHash cannot be null.");

        var user = new User
        {
            Id = id,
            Username = username,
            FullName = fullName,
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
            CreatedAt = nowUtc
        };

        user.Raise(new UserCreatedDomainEvent(id, username, role, nowUtc));

        return user;
    }
}
