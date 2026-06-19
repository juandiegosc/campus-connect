namespace Attendance.Application.Incidents.ReportIncident;

/// <summary>Response DTO for the ReportIncident command.</summary>
public sealed record ReportIncidentResponse(string IncidentId, string Severity);
