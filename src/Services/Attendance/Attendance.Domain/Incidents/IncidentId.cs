using BuildingBlocks.Application.Common;
using NUlid;

namespace Attendance.Domain.Incidents;

/// <summary>
/// Strongly-typed ULID identifier for the Incident aggregate.
/// 26-character base32 string. NUlid 1.7.3 API: Ulid.NewUlid(DateTimeOffset).
/// </summary>
public sealed class IncidentId : IEquatable<IncidentId>
{
    public string Value { get; }

    private IncidentId(string value) => Value = value;

    public static IncidentId New(DateTimeOffset timestamp)
        => new(Ulid.NewUlid(timestamp).ToString());

    /// <summary>
    /// For EF Core value converter — reads a value that was previously persisted by this type.
    /// </summary>
    public static IncidentId FromRaw(string value) => new(value);

    public static Result<IncidentId> TryCreate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length != 26)
            return Result<IncidentId>.Failure(
                Error.Validation("incident_id.invalid",
                    $"IncidentId '{raw}' must be a 26-character ULID string."));
        return Result<IncidentId>.Success(new IncidentId(raw));
    }

    public bool Equals(IncidentId? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is IncidentId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    public override string ToString() => Value;

    public static bool operator ==(IncidentId? left, IncidentId? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(IncidentId? left, IncidentId? right)
        => !(left == right);
}
