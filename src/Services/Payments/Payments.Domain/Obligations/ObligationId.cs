using BuildingBlocks.Domain.Exceptions;
using NUlid;

namespace Payments.Domain.Obligations;

/// <summary>
/// Strongly-typed ULID identifier for the <see cref="Obligation"/> aggregate.
/// 26-character base32 string. DISTINCT type from PaymentId (ESC-PM-25 — one-way door).
/// NUlid 1.7.3 API: Ulid.NewUlid(DateTimeOffset) — Gotcha 1.
/// </summary>
public sealed class ObligationId : IEquatable<ObligationId>
{
    public string Value { get; }

    private ObligationId(string value) => Value = value;

    public static ObligationId New(DateTimeOffset timestamp)
        => new(Ulid.NewUlid(timestamp).ToString());

    public static ObligationId Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 26)
            throw new DomainException($"Invalid ObligationId '{value}': must be a 26-character ULID string.");
        return new ObligationId(value);
    }

    public bool Equals(ObligationId? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is ObligationId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    public override string ToString() => Value;

    public static bool operator ==(ObligationId? left, ObligationId? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(ObligationId? left, ObligationId? right)
        => !(left == right);
}
