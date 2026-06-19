using Attendance.Application.Abstractions;
using Attendance.Domain.Incidents;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Contracts.Events;
using MassTransit;
using MediatR;

namespace Attendance.Application.Incidents.ReportIncident;

/// <summary>
/// Handler for ReportIncidentCommand.
/// Mirrors RecordAttendanceCommandHandler flow (ADR-075, REQ-AT1-20).
/// CRITICAL: IncidentReported event has NO description (REQ-AT1-13, one-way door).
/// </summary>
public sealed class ReportIncidentCommandHandler(
    IIncidentRepository       repo,
    IStudentReplicaRepository students,
    IPublishEndpoint          publishEndpoint)
    : IRequestHandler<ReportIncidentCommand, Result<ReportIncidentResponse>>
{
    public async Task<Result<ReportIncidentResponse>> Handle(
        ReportIncidentCommand command,
        CancellationToken     cancellationToken)
    {
        // 1. Existence guard (REQ-AT1-21, ADR-056)
        if (!await students.ExistsAsync(command.StudentId, cancellationToken))
            return Result<ReportIncidentResponse>.Failure(
                Error.Validation("student.not_found",
                    $"Student {command.StudentId} is not known to Attendance service."));

        // 2. Parse severity enum
        var severityResult = IncidentSeverityExtensions.TryCreate(command.Severity);
        if (!severityResult.IsSuccess)
            return Result<ReportIncidentResponse>.Failure(severityResult.Error);

        // 3. Generate ID
        var now = DateTimeOffset.UtcNow;
        var id  = IncidentId.New(now);

        // 4. Create aggregate
        var incidentResult = Incident.Report(
            command.StudentId,
            command.Type,
            severityResult.Value,
            command.Description,
            now.UtcDateTime,
            id);

        if (!incidentResult.IsSuccess)
            return Result<ReportIncidentResponse>.Failure(incidentResult.Error);

        // 5. Persist
        await repo.AddAsync(incidentResult.Value, cancellationToken);

        // 6. ★ CRITICAL (ADR-075, REQ-AT1-20): Publish BEFORE returning
        // Description intentionally absent (REQ-AT1-13 one-way door).
        await publishEndpoint.Publish(new IncidentReported
        {
            IncidentId = id.Value,
            StudentId  = command.StudentId,
            Type       = command.Type,
            Severity   = severityResult.Value.ToString()
        }, cancellationToken);

        // 7. Return
        return Result<ReportIncidentResponse>.Success(
            new ReportIncidentResponse(id.Value, severityResult.Value.ToString()));
    }
}
