using BuildingBlocks.Domain.Exceptions;

namespace Identity.Domain.Users;

/// <summary>
/// Strongly-typed identifier for the <see cref="User"/> aggregate root.
/// Wraps a <see cref="Guid"/> to prevent primitive obsession.
/// </summary>
/// <param name="Value">The underlying <see cref="Guid"/> value.</param>
public readonly record struct UserId(Guid Value)
{
    /// <summary>Creates a new <see cref="UserId"/> with a freshly generated GUID.</summary>
    /// <returns>A new <see cref="UserId"/> with a non-empty value.</returns>
    public static UserId New() => new(Guid.NewGuid());

    /// <summary>
    /// Creates a <see cref="UserId"/> from an existing <see cref="Guid"/> value.
    /// </summary>
    /// <param name="value">The GUID to wrap.</param>
    /// <returns>A <see cref="UserId"/> wrapping the provided <paramref name="value"/>.</returns>
    /// <exception cref="DomainException">Thrown when <paramref name="value"/> is <see cref="Guid.Empty"/>.</exception>
    public static UserId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainException("UserId cannot be an empty GUID.");

        return new UserId(value);
    }
}
