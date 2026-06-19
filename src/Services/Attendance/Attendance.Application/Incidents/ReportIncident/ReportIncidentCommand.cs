using BuildingBlocks.Application.Messaging;

namespace Attendance.Application.Incidents.ReportIncident;

/// <summary>
/// Command to report an incident (REQ-AT1-17).
/// ICommand trigger activates UnitOfWorkBehavior (REQ-AT1-18).
/// </summary>
public sealed record ReportIncidentCommand(
    string StudentId,
    string Type,
    string Severity,
    string Description) : ICommand<ReportIncidentResponse>;
