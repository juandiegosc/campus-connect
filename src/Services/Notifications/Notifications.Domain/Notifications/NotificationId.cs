using BuildingBlocks.Application.Common;
using NUlid;

namespace Notifications.Domain.Notifications;

/// <summary>
/// Strongly-typed ULID identifier for the Notification aggregate (26-character base32 string).
/// </summary>
public sealed class NotificationId : IEquatable<NotificationId>
{
    public string Value { get; }

    private NotificationId(string value) => Value = value;

    public static NotificationId New(DateTimeOffset timestamp)
        => new(Ulid.NewUlid(timestamp).ToString());

    /// <summary>For EF Core value converter — reads a previously persisted value.</summary>
    public static NotificationId FromRaw(string value) => new(value);

    public static Result<NotificationId> TryCreate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length != 26)
            return Result<NotificationId>.Failure(
                Error.Validation("notification_id.invalid",
                    $"NotificationId '{raw}' must be a 26-character ULID string."));
        return Result<NotificationId>.Success(new NotificationId(raw));
    }

    public bool Equals(NotificationId? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is NotificationId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    public override string ToString() => Value;

    public static bool operator ==(NotificationId? left, NotificationId? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(NotificationId? left, NotificationId? right)
        => !(left == right);
}
