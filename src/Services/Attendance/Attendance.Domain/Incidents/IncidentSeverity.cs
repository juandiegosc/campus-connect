using BuildingBlocks.Application.Common;

namespace Attendance.Domain.Incidents;

/// <summary>
/// Incident severity enum with exactly three members (REQ-AT1-05).
/// TryCreate returns Result.Failure for unknown values (REQ-AT1-12).
/// </summary>
public enum IncidentSeverity
{
    Low,
    Medium,
    High
}

public static class IncidentSeverityExtensions
{
    public static Result<IncidentSeverity> TryCreate(string? raw)
    {
        if (Enum.TryParse<IncidentSeverity>(raw, ignoreCase: true, out var value))
            return Result<IncidentSeverity>.Success(value);

        return Result<IncidentSeverity>.Failure(
            Error.Validation("incident_severity.invalid",
                $"Severity '{raw}' is not valid. Must be one of: {string.Join(", ", Enum.GetNames<IncidentSeverity>())}."));
    }
}
