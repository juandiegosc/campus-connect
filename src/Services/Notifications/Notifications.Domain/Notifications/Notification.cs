using BuildingBlocks.Application.Common;
using BuildingBlocks.Domain.Primitives;

namespace Notifications.Domain.Notifications;

/// <summary>
/// Notification aggregate root. Represents a single (simulated) notification produced in
/// reaction to a domain event (Pub/Sub) or an ad-hoc request (Point-to-Point).
///
/// A notification is created already RESOLVED: either Sent or Failed. The delivery itself is
/// simulated — no real e-mail/SMS gateway is involved (academic project scope).
/// SchoolId hardcoded "SCH-001" — // TODO multi-tenant.
/// </summary>
public sealed class Notification : AggregateRoot<NotificationId>
{
    public string SourceEvent { get; private set; } = default!;
    public string? StudentId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Recipient { get; private set; } = default!;
    public string Subject { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public NotificationStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string SchoolId { get; private set; } = default!; // TODO multi-tenant

    // EF Core parameterless constructor
    private Notification() { }

    /// <summary>
    /// Factory for a successfully delivered notification.
    /// </summary>
    public static Result<Notification> CreateSent(
        NotificationId id,
        string sourceEvent,
        string? studentId,
        NotificationChannel channel,
        string recipient,
        string subject,
        string body,
        DateTime nowUtc)
    {
        var baseResult = Validate(sourceEvent, recipient, subject);
        if (!baseResult.IsSuccess)
            return Result<Notification>.Failure(baseResult.Error);

        return Result<Notification>.Success(new Notification
        {
            Id = id,
            SourceEvent = sourceEvent.Trim(),
            StudentId = string.IsNullOrWhiteSpace(studentId) ? null : studentId.Trim(),
            Channel = channel,
            Recipient = recipient.Trim(),
            Subject = subject.Trim(),
            Body = body?.Trim() ?? string.Empty,
            Status = NotificationStatus.Sent,
            FailureReason = null,
            CreatedAt = nowUtc,
            SchoolId = "SCH-001" // TODO multi-tenant
        });
    }

    /// <summary>
    /// Factory for a failed notification. Records the failure reason for later analytics.
    /// </summary>
    public static Notification CreateFailed(
        NotificationId id,
        string sourceEvent,
        string? studentId,
        NotificationChannel channel,
        string recipient,
        string subject,
        string body,
        string reason,
        DateTime nowUtc)
        => new()
        {
            Id = id,
            SourceEvent = string.IsNullOrWhiteSpace(sourceEvent) ? "Unknown" : sourceEvent.Trim(),
            StudentId = string.IsNullOrWhiteSpace(studentId) ? null : studentId.Trim(),
            Channel = channel,
            Recipient = recipient?.Trim() ?? string.Empty,
            Subject = subject?.Trim() ?? string.Empty,
            Body = body?.Trim() ?? string.Empty,
            Status = NotificationStatus.Failed,
            FailureReason = string.IsNullOrWhiteSpace(reason) ? "Unknown failure" : reason.Trim(),
            CreatedAt = nowUtc,
            SchoolId = "SCH-001" // TODO multi-tenant
        };

    private static Result<bool> Validate(string sourceEvent, string recipient, string subject)
    {
        if (string.IsNullOrWhiteSpace(sourceEvent))
            return Result<bool>.Failure(Error.Validation("notification.source_required", "SourceEvent is required."));
        if (string.IsNullOrWhiteSpace(recipient))
            return Result<bool>.Failure(Error.Validation("notification.recipient_required", "Recipient is required."));
        if (string.IsNullOrWhiteSpace(subject))
            return Result<bool>.Failure(Error.Validation("notification.subject_required", "Subject is required."));
        return Result<bool>.Success(true);
    }
}
