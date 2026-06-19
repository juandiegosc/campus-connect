using MediatR;
using Microsoft.AspNetCore.Mvc;
using Payments.Application.Students.GetStudents;
using Payments.Application.Students.Shared;
using BuildingBlocks.Application.Common;

namespace Payments.API.Endpoints;

/// <summary>
/// Minimal API endpoints for GET /api/payments/students.
/// Guarded by "Finanzas" authorization policy (REQ-PM2-06).
/// Separate route group from /api/payments/obligations (ADR-059 R6).
/// </summary>
public static class StudentEndpoints
{
    public static IEndpointRouteBuilder MapStudentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments/students")
            .RequireAuthorization("Finanzas")
            .WithTags("Payments.Students");

        // GET /api/payments/students — paginated + optional grade/search filters (REQ-PM2-06..09)
        group.MapGet("/", async (
            [FromQuery] int     page     = 1,
            [FromQuery] int     pageSize = 20,
            [FromQuery] string? grade    = null,
            [FromQuery] string? search   = null,
            ISender             sender   = null!,
            CancellationToken   ct       = default) =>
        {
            var result = await sender.Send(
                new GetStudentsQuery(page, pageSize, grade, search), ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        })
        .WithName("GetPaymentStudents")
        .WithSummary("Listar réplica local de estudiantes (paginado)")
        .WithDescription(
            "Devuelve la réplica local de estudiantes sincronizada vía los eventos `StudentEnrolled` y `StudentStatusUpdated`. " +
            "Query params: `page` (default 1), `pageSize` (default 20), `grade` (filtro por grado exacto, opcional), " +
            "`search` (subcadena del nombre, opcional). " +
            "Cada ítem incluye `studentId`, `fullName`, `grade`, `schoolId`, `lastUpdatedAt`, " +
            "`academicStatus` y `financialStatus` (pueden ser null en réplicas pre-fase-3). " +
            "Útil para validar `studentId` al registrar una obligación. " +
            "Rol requerido: **Finanzas**.")
        .Produces<PagedList<StudentReplicaItemDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        return app;
    }

    // Includes error.Code in the detail so clients can distinguish error subtypes programmatically.
    // This is especially important for student.not_found (ADR-056) vs generic validation failures.
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
