using BuildingBlocks.Domain.Exceptions;
using NUlid;

namespace Academic.Domain.Students;

/// <summary>
/// Strongly-typed ULID identifier for the <see cref="Student"/> aggregate.
/// 26-character base32 string, lexicographically ordered by timestamp (ADR-036).
/// No STU- prefix — that belongs to the presentation layer if needed.
/// </summary>
public sealed class StudentId : IEquatable<StudentId>
{
    public string Value { get; }

    private StudentId(string value) => Value = value;

    /// <summary>Creates a new StudentId using the NUlid library with the given timestamp for ordering.</summary>
    public static StudentId New(DateTimeOffset timestamp)
        => new(Ulid.NewUlid(timestamp).ToString());

    /// <summary>Parses an existing ULID string — validates that it is exactly 26 chars.</summary>
    /// <exception cref="DomainException">Thrown when the value is not a valid 26-char ULID.</exception>
    public static StudentId Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 26)
            throw new DomainException($"Invalid StudentId '{value}': must be a 26-character ULID string.");
        return new StudentId(value);
    }

    public bool Equals(StudentId? other)
        => other is not null && Value == other.Value;

    public override bool Equals(object? obj)
        => obj is StudentId other && Equals(other);

    public override int GetHashCode()
        => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;

    public static bool operator ==(StudentId? left, StudentId? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(StudentId? left, StudentId? right)
        => !(left == right);
}
