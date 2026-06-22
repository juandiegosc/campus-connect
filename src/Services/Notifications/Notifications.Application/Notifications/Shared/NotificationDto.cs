namespace Notifications.Application.Notifications.Shared;

/// <summary>
/// Read DTO for GET /api/notifications.
/// </summary>
public sealed record NotificationDto(
    string NotificationId,
    string SourceEvent,
    string? StudentId,
    string Channel,
    string Recipient,
    string Subject,
    string Body,
    string Status,
    string? FailureReason,
    DateTime CreatedAt);
