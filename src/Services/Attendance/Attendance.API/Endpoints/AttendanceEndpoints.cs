using Attendance.Application.Attendance.RecordAttendance;
using Attendance.Application.Incidents.ReportIncident;
using Attendance.Application.Students.GetStudentHistory;
using Attendance.Application.Students.GetStudents;
using BuildingBlocks.Application.Common;
using MediatR;

namespace Attendance.API.Endpoints;

/// <summary>
/// Minimal API endpoints for Attendance service (REQ-AT1-37..44).
/// 4 routes: POST records, POST incidents, GET students, GET students/{id}/history.
/// Handlers never read HttpContext — all inputs extracted here and passed via commands (REQ-AT1-25).
/// </summary>
public static class AttendanceEndpoints
{
    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /api/attendance/records — Docente only (REQ-AT1-37, REQ-AT1-34)
        app.MapPost("/api/attendance/records",
            async (RecordAttendanceRequest req, ISender sender, CancellationToken ct) =>
            {
                var cmd    = new RecordAttendanceCommand(req.StudentId, req.Date, req.Status);
                var result = await sender.Send(cmd, ct);
                return result.IsSuccess
                    ? Results.Created($"/api/attendance/records/{result.Value.RecordId}", result.Value)
                    : MapError(result.Error);
            })
            .RequireAuthorization("Docente")
            .WithTags("Attendance.Records")
            .WithName("RecordAttendance")
            .Produces<RecordAttendanceResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // POST /api/attendance/incidents — Docente only (REQ-AT1-40, REQ-AT1-34)
        app.MapPost("/api/attendance/incidents",
            async (ReportIncidentRequest req, ISender sender, CancellationToken ct) =>
            {
                var cmd    = new ReportIncidentCommand(req.StudentId, req.Type, req.Severity, req.Description);
                var result = await sender.Send(cmd, ct);
                return result.IsSuccess
                    ? Results.Created($"/api/attendance/incidents/{result.Value.IncidentId}", result.Value)
                    : MapError(result.Error);
            })
            .RequireAuthorization("Docente")
            .WithTags("Attendance.Incidents")
            .WithName("ReportIncident")
            .Produces<ReportIncidentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // GET /api/attendance/students — Docente only (REQ-AT1-43, REQ-AT1-34)
        app.MapGet("/api/attendance/students",
            async (ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetStudentsQuery(), ct);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : MapError(result.Error);
            })
            .RequireAuthorization("Docente")
            .WithTags("Attendance.Students")
            .WithName("GetAttendanceStudents")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // GET /api/attendance/students/{id}/history — Docente OR Direccion (REQ-AT1-44, REQ-AT1-34)
        app.MapGet("/api/attendance/students/{id}/history",
            async (string id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetStudentHistoryQuery(id), ct);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : MapError(result.Error);
            })
            .RequireAuthorization("DocenteOrDireccion")
            .WithTags("Attendance.Students")
            .WithName("GetStudentHistory")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }

    // Mirrors PaymentEndpoints.MapError — includes error code in detail (ADR-056)
    private static IResult MapError(Error error) => error.Type switch
    {
        ErrorType.NotFound    => Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status404NotFound,           title: "Not Found"),
        ErrorType.Conflict    => Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status409Conflict,           title: "Conflict"),
        ErrorType.Validation  => Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status400BadRequest,         title: "Validation Error"),
        ErrorType.Unauthorized=> Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status401Unauthorized,       title: "Unauthorized"),
        ErrorType.Forbidden   => Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status403Forbidden,          title: "Forbidden"),
        _                     => Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status500InternalServerError, title: "Internal Server Error")
    };
}

/// <summary>Request body for POST /api/attendance/records.</summary>
public sealed record RecordAttendanceRequest(
    string StudentId,
    string Date,
    string Status);

/// <summary>Request body for POST /api/attendance/incidents.</summary>
public sealed record ReportIncidentRequest(
    string StudentId,
    string Type,
    string Severity,
    string Description);
