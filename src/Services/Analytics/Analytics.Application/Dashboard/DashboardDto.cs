namespace Analytics.Application.Dashboard;

/// <summary>
/// Aggregated dashboard read model for the school director (REQ analytics).
/// All values are derived from the analytics projections (ProcessedEvent + StudentProjection).
/// </summary>
public sealed record DashboardDto(
    int TotalStudents,
    int PaymentsConfirmed,
    int PaymentsPending,
    int AttendanceRecorded,
    int IncidentsReported,
    int NotificationsSent,
    int EventsProcessed,
    int FailedMessages,
    string Status,
    DateTime GeneratedAt);
