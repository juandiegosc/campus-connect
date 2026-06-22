using BuildingBlocks.Contracts.Commands;
using BuildingBlocks.Application.Common;
using MassTransit;
using MediatR;
using Notifications.Application.Notifications.GetNotifications;
using Notifications.Application.Notifications.Shared;

namespace Notifications.API.Endpoints;

/// <summary>
/// Minimal API endpoints for the Notifications service.
/// - GET  /api/notifications        → recent notification log (any authenticated role)
/// - POST /api/notifications/send    → Point-to-Point: SENDS a SendNotificationCommand to the queue
/// </summary>
public static class NotificationsEndpoints
{
    // Endpoint name formatter (KebabCase, prefix "notifications") maps SendNotificationConsumer
    // to the queue "notifications-send-notification".
    private static readonly Uri SendQueue = new("queue:notifications-send-notification");

    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/notifications",
            async (ISender sender, int? take, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetNotificationsQuery(take ?? 100), ct);
                return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
            })
            .RequireAuthorization()
            .WithTags("Notifications")
            .WithName("GetNotifications")
            .WithSummary("Listar notificaciones recientes")
            .WithDescription(
                "Devuelve el registro de notificaciones generadas por el servicio (enviadas y fallidas), " +
                "ordenadas de la más reciente a la más antigua. " +
                "Cada notificación incluye el evento de origen, canal, destinatario, estado y motivo de fallo si aplica. " +
                "Parámetro opcional `take` (1-500, por defecto 100). " +
                "Requiere autenticación JWT (cualquier rol).")
            .Produces<IReadOnlyList<NotificationDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/api/notifications/send",
            async (SendNotificationRequest req, IBus bus, CancellationToken ct) =>
            {
                // Use the singleton IBus (not the scoped ISendEndpointProvider) so the Send goes
                // straight to the broker. The scoped provider would be captured by the EF bus outbox
                // and, without a DbContext SaveChanges in this request, never delivered.
                var endpoint = await bus.GetSendEndpoint(SendQueue);
                await endpoint.Send(new SendNotificationCommand
                {
                    Recipient = req.Recipient,
                    Channel = string.IsNullOrWhiteSpace(req.Channel) ? "Email" : req.Channel,
                    Subject = req.Subject,
                    Body = req.Body,
                    CorrelationId = Guid.NewGuid().ToString()
                }, ct);

                return Results.Accepted();
            })
            .RequireAuthorization("Direccion")
            .WithTags("Notifications")
            .WithName("SendNotification")
            .WithSummary("Enviar una notificación ad-hoc (Point-to-Point)")
            .WithDescription(
                "Encola un `SendNotificationCommand` directamente en la cola del servicio " +
                "(patrón Point-to-Point: un único consumidor procesa el mensaje). " +
                "El servicio simula el envío y registra la notificación resultante (Sent o Failed). " +
                "`channel` valores válidos: `Email`, `Sms`, `Push`. " +
                "Rol requerido: **Direccion**.")
            .Produces(StatusCodes.Status202Accepted)
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

/// <summary>Request body for POST /api/notifications/send.</summary>
public sealed record SendNotificationRequest(
    string Recipient,
    string? Channel,
    string Subject,
    string Body);
