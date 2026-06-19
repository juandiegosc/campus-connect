using BuildingBlocks.Application.Common;
using NUlid;

namespace Attendance.Domain.Attendance;

/// <summary>
/// Strongly-typed ULID identifier for the AttendanceRecord aggregate.
/// 26-character base32 string. NUlid 1.7.3 API: Ulid.NewUlid(DateTimeOffset).
/// </summary>
public sealed class AttendanceRecordId : IEquatable<AttendanceRecordId>
{
    public string Value { get; }

    private AttendanceRecordId(string value) => Value = value;

    public static AttendanceRecordId New(DateTimeOffset timestamp)
        => new(Ulid.NewUlid(timestamp).ToString());

    /// <summary>
    /// For EF Core value converter — reads a value that was previously persisted by this type.
    /// Trusts the value is valid (EF always reads what it wrote).
    /// </summary>
    public static AttendanceRecordId FromRaw(string value) => new(value);

    public static Result<AttendanceRecordId> TryCreate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length != 26)
            return Result<AttendanceRecordId>.Failure(
                Error.Validation("attendance_record_id.invalid",
                    $"AttendanceRecordId '{raw}' must be a 26-character ULID string."));
        return Result<AttendanceRecordId>.Success(new AttendanceRecordId(raw));
    }

    public bool Equals(AttendanceRecordId? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is AttendanceRecordId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    public override string ToString() => Value;

    public static bool operator ==(AttendanceRecordId? left, AttendanceRecordId? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(AttendanceRecordId? left, AttendanceRecordId? right)
        => !(left == right);
}
