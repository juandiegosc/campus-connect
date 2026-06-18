using System.Text.RegularExpressions;
using BuildingBlocks.Domain.Exceptions;

namespace Academic.Domain.Students;

/// <summary>
/// Value object for student document identifiers.
/// Format constraint (Q5): ^[A-Za-z0-9]{6,15}$
/// Factory returns null on failure to avoid Application layer dependency in Domain.
/// </summary>
public sealed partial class DocumentId : IEquatable<DocumentId>
{
    [GeneratedRegex(@"^[A-Za-z0-9]{6,15}$", RegexOptions.Compiled)]
    private static partial Regex DocumentIdRegex();

    public string Value { get; }

    private DocumentId(string value) => Value = value;

    /// <summary>
    /// Creates a DocumentId from the given raw string.
    /// Returns (null, errorMessage) if validation fails.
    /// </summary>
    public static (DocumentId? Result, string? Error) TryCreate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !DocumentIdRegex().IsMatch(raw))
            return (null, "DocumentId must be 6-15 alphanumeric characters (^[A-Za-z0-9]{6,15}$).");

        return (new DocumentId(raw), null);
    }

    /// <summary>Parses a DocumentId, throwing DomainException on failure.</summary>
    public static DocumentId Parse(string raw)
    {
        var (result, error) = TryCreate(raw);
        if (result is null)
            throw new DomainException(error!);
        return result;
    }

    public bool Equals(DocumentId? other)
        => other is not null && Value == other.Value;

    public override bool Equals(object? obj)
        => obj is DocumentId other && Equals(other);

    public override int GetHashCode()
        => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;

    public static bool operator ==(DocumentId? left, DocumentId? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(DocumentId? left, DocumentId? right)
        => !(left == right);
}
