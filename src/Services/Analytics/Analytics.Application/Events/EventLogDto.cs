namespace Analytics.Application.Events;

/// <summary>
/// Read DTO for the processed-events log (GET /api/analytics/events).
/// </summary>
public sealed record EventLogDto(
    string EventType,
    string? EntityId,
    string? CorrelationId,
    DateTime OccurredAt,
    DateTime ReceivedAt);
