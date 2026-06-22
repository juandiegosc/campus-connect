using Analytics.Application.Dashboard;
using Analytics.Application.Dashboard.GetDashboard;
using Analytics.Application.Events;
using Analytics.Application.Events.GetEvents;
using BuildingBlocks.Application.Common;
using MediatR;

namespace Analytics.API.Endpoints;

/// <summary>
/// Minimal API endpoints for the Analytics service (CQRS read side).
/// - GET /api/analytics/dashboard → aggregated metrics for the director dashboard
/// - GET /api/analytics/events    → processed-events log
/// </summary>
public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/analytics/dashboard",
            async (ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetDashboardQuery(), ct);
                return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
            })
            .RequireAuthorization("Direccion")
            .WithTags("Analytics")
            .WithName("GetDashboard")
            .WithSummary("Obtener el tablero analítico de la institución")
            .WithDescription(
                "Devuelve métricas agregadas calculadas a partir de los eventos consumidos: " +
                "total de estudiantes matriculados, pagos confirmados, pagos pendientes, asistencias registradas, " +
                "incidentes reportados, notificaciones enviadas, eventos procesados, mensajes fallidos y estado general. " +
                "Rol requerido: **Direccion**.")
            .Produces<DashboardDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        app.MapGet("/api/analytics/events",
            async (ISender sender, int? take, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetEventsQuery(take ?? 100), ct);
                return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
            })
            .RequireAuthorization("Direccion")
            .WithTags("Analytics")
            .WithName("GetAnalyticsEvents")
            .WithSummary("Listar los eventos procesados por el pipeline analítico")
            .WithDescription(
                "Devuelve el registro de eventos de integración ingeridos (tipo, entidad, correlación, fechas), " +
                "ordenados del más reciente al más antiguo. " +
                "Parámetro opcional `take` (1-500, por defecto 100). " +
                "Rol requerido: **Direccion**.")
            .Produces<IReadOnlyList<EventLogDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }

    private static IResult MapError(Error error) => error.Type switch
    {
        ErrorType.NotFound => Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status404NotFound, title: "Not Found"),
        ErrorType.Conflict => Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status409Conflict, title: "Conflict"),
        ErrorType.Validation => Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status400BadRequest, title: "Validation Error"),
        ErrorType.Unauthorized => Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized"),
        ErrorType.Forbidden => Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status403Forbidden, title: "Forbidden"),
        _ => Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status500InternalServerError, title: "Internal Server Error")
    };
}
