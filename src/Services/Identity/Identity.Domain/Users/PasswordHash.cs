using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Primitives;

namespace Identity.Domain.Users;

/// <summary>
/// Value object that wraps an already-hashed password string.
/// Does NOT know about BCrypt — the application layer computes the hash
/// via <c>IPasswordHasher</c> and passes the result to <see cref="Create"/>.
/// </summary>
public sealed class PasswordHash : ValueObject
{
    /// <summary>The raw hash string stored in the database.</summary>
    public string Value { get; }

    private PasswordHash(string value) => Value = value;

    /// <summary>
    /// Creates a <see cref="PasswordHash"/> wrapping the provided hash string.
    /// </summary>
    /// <param name="hash">A non-null, non-whitespace hashed password string.</param>
    /// <returns>A <see cref="PasswordHash"/> instance.</returns>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="hash"/> is <c>null</c>, empty, or whitespace.
    /// </exception>
    public static PasswordHash Create(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new DomainException("PasswordHash cannot be null or empty.");

        return new PasswordHash(hash);
    }

    /// <inheritdoc />
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
