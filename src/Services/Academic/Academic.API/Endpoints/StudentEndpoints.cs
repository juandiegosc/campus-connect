using Academic.Application.Students.EnrollStudent;
using Academic.Application.Students.GetStudentById;
using Academic.Application.Students.GetStudentEvents;
using Academic.Application.Students.GetStudentStatus;
using Academic.Application.Students.GetStudents;
using BuildingBlocks.Application.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Academic.API.Endpoints;

/// <summary>
/// Minimal API endpoint definitions for Academic student management.
/// 5 endpoints per docs/02 §2 and REQ-AC1-21..REQ-AC1-26.
/// IHttpContextAccessor MUST NOT appear in this file — claims are read from HttpContext directly.
/// </summary>
public static class StudentEndpoints
{
    public static IEndpointRouteBuilder MapStudentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/academic/students").WithTags("Academic.Students");

        // POST /api/academic/students — requires Secretaria role (ESC-35, ESC-36, ESC-38)
        group.MapPost("/", async (
            [FromBody] EnrollStudentRequest req,
            ISender sender,
            CancellationToken ct) =>
        {
            var cmd = new EnrollStudentCommand(
                req.FullName,
                req.DocumentId,
                req.Grade,
                req.SchoolId ?? "SCH-001",
                req.GuardianName,
                req.GuardianEmail);

            var result = await sender.Send(cmd, ct);

            return result.IsSuccess
                ? Results.Created($"/api/academic/students/{result.Value.StudentId}", result.Value)
                : MapError(result.Error);
        })
        .RequireAuthorization("Secretaria")
        .WithName("EnrollStudent")
        .Produces<EnrollStudentResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // GET /api/academic/students — requires Secretaria or Direccion (ESC-27)
        group.MapGet("/", async (
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? grade = null,
            [FromQuery] string? search = null,
            ISender sender = null!,
            CancellationToken ct = default) =>
        {
            var result = await sender.Send(new GetStudentsQuery(page, pageSize, grade, search), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        })
        .RequireAuthorization("SecretariaOrDireccion")
        .WithName("GetStudents")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // GET /api/academic/students/{id} — requires Secretaria or Direccion (ESC-25, ESC-26)
        group.MapGet("/{id}", async (
            string id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetStudentByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        })
        .RequireAuthorization("SecretariaOrDireccion")
        .WithName("GetStudentById")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // GET /api/academic/students/{id}/status — any authenticated user, no role (ESC-37, Q3)
        group.MapGet("/{id}/status", async (
            string id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetStudentStatusQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        })
        .RequireAuthorization()  // no role policy — any authenticated user (ESC-37, G10)
        .WithName("GetStudentStatus")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        // GET /api/academic/students/{id}/events — requires Secretaria or Direccion (ADR-036, R10)
        group.MapGet("/{id}/events", async (
            string id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetStudentEventsQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(new { items = result.Value })
                : MapError(result.Error);
        })
        .RequireAuthorization("SecretariaOrDireccion")
        .WithName("GetStudentEvents")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        return app;
    }

    /// <summary>Maps domain errors to RFC 7807 ProblemDetails HTTP results.</summary>
    private static IResult MapError(Error error) => error.Type switch
    {
        ErrorType.NotFound   => Results.Problem(detail: error.Message, statusCode: StatusCodes.Status404NotFound,   title: "Not Found"),
        ErrorType.Conflict   => Results.Problem(detail: error.Message, statusCode: StatusCodes.Status409Conflict,   title: "Conflict"),
        ErrorType.Validation => Results.Problem(detail: error.Message, statusCode: StatusCodes.Status400BadRequest, title: "Validation Error"),
        ErrorType.Unauthorized => Results.Problem(detail: error.Message, statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized"),
        ErrorType.Forbidden  => Results.Problem(detail: error.Message, statusCode: StatusCodes.Status403Forbidden,  title: "Forbidden"),
        _                    => Results.Problem(detail: error.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Internal Server Error")
    };
}

/// <summary>Request body for POST /api/academic/students.</summary>
public sealed record EnrollStudentRequest(
    string  FullName,
    string  DocumentId,
    string  Grade,
    string? SchoolId,
    string  GuardianName,
    string  GuardianEmail);
