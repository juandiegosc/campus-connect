using Analytics.Application.Dashboard;
using Analytics.Application.Events;

namespace Analytics.Application.Abstractions;

/// <summary>
/// Port for analytics projection persistence and queries.
/// Write methods are called DIRECTLY by MassTransit consumers (outside the MediatR UoW pipeline)
/// and therefore commit their own SaveChanges. They are idempotent: a duplicate EventId is ignored.
/// </summary>
public interface IAnalyticsRepository
{
    /// <summary>
    /// Records a generic processed event (idempotent on <paramref name="eventId"/>). COMMITS.
    /// Returns true if the event was newly recorded, false if it was a duplicate.
    /// </summary>
    Task<bool> RecordEventAsync(
        Guid eventId,
        string eventType,
        string? entityId,
        string? correlationId,
        DateTime occurredAt,
        CancellationToken ct = default);

    /// <summary>
    /// Records a StudentEnrolled event AND upserts the student projection in one transaction.
    /// Idempotent on <paramref name="eventId"/>. COMMITS.
    /// </summary>
    Task RecordStudentEnrolledAsync(
        Guid eventId,
        string? correlationId,
        DateTime occurredAt,
        string studentId,
        string fullName,
        string grade,
        CancellationToken ct = default);

    /// <summary>
    /// Records a StudentStatusUpdated event AND updates the student projection status in one transaction.
    /// Idempotent on <paramref name="eventId"/>. COMMITS.
    /// </summary>
    Task RecordStudentStatusUpdatedAsync(
        Guid eventId,
        string? correlationId,
        DateTime occurredAt,
        string studentId,
        string academicStatus,
        string financialStatus,
        CancellationToken ct = default);

    /// <summary>Computes the dashboard aggregate from the projections.</summary>
    Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default);

    /// <summary>Returns the most recently processed events (newest first).</summary>
    Task<IReadOnlyList<EventLogDto>> GetRecentEventsAsync(int take, CancellationToken ct = default);
}
