using BuildingBlocks.Domain.Exceptions;
using NUlid;

namespace Payments.Domain.Obligations;

/// <summary>
/// Strongly-typed ULID identifier for the embedded <see cref="Payment"/> entity.
/// DISTINCT type from <see cref="ObligationId"/> — never interchangeable (ESC-PM-25 one-way door).
/// NUlid 1.7.3 API: Ulid.NewUlid(DateTimeOffset) — Gotcha 1.
/// </summary>
public sealed class PaymentId : IEquatable<PaymentId>
{
    public string Value { get; }

    private PaymentId(string value) => Value = value;

    public static PaymentId New(DateTimeOffset timestamp)
        => new(Ulid.NewUlid(timestamp).ToString());

    public static PaymentId Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 26)
            throw new DomainException($"Invalid PaymentId '{value}': must be a 26-character ULID string.");
        return new PaymentId(value);
    }

    public bool Equals(PaymentId? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is PaymentId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    public override string ToString() => Value;

    public static bool operator ==(PaymentId? left, PaymentId? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(PaymentId? left, PaymentId? right)
        => !(left == right);
}
