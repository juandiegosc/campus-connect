using BuildingBlocks.Application.Common;
using MediatR;
using Payments.Application.Obligations.ConfirmPayment;
using Payments.Application.Obligations.GetObligationById;
using Payments.Application.Obligations.GetObligations;
using Payments.Application.Obligations.RegisterObligation;

namespace Payments.API.Endpoints;

/// <summary>
/// Minimal API endpoints for Payments obligation management.
/// 4 endpoints per REQ-PM1-01..REQ-PM1-10.
/// All guarded by "Finanzas" authorization policy.
/// </summary>
public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments/obligations")
            .RequireAuthorization("Finanzas")
            .WithTags("Payments.Obligations");

        // POST /api/payments/obligations — Register obligation (REQ-PM1-01)
        group.MapPost("/", async (RegisterObligationRequest req, ISender sender, CancellationToken ct) =>
        {
            var cmd = new RegisterObligationCommand(req.StudentId, req.Concept, req.Amount, req.DueDate);
            var result = await sender.Send(cmd, ct);
            return result.IsSuccess
                ? Results.Created($"/api/payments/obligations/{result.Value.ObligationId}", result.Value)
                : MapError(result.Error);
        })
        .WithName("RegisterObligation")
        .Produces<RegisterObligationResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // POST /api/payments/obligations/{id}/confirm — Confirm payment (REQ-PM1-04)
        group.MapPost("/{id}/confirm", async (string id, ConfirmPaymentRequest req, ISender sender, CancellationToken ct) =>
        {
            var cmd = new ConfirmPaymentCommand(id, req.Method, req.Reference);
            var result = await sender.Send(cmd, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        })
        .WithName("ConfirmPayment")
        .Produces<ConfirmPaymentResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // GET /api/payments/obligations?status= — List obligations (REQ-PM1-09)
        group.MapGet("/", async (string? status, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetObligationsQuery(status), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        })
        .WithName("GetObligations")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // GET /api/payments/obligations/{id} — Get obligation by id (REQ-PM1-10)
        group.MapGet("/{id}", async (string id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetObligationByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        })
        .WithName("GetObligationById")
        .Produces<ObligationDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        return app;
    }

    // Includes error.Code in the detail for programmatic client discrimination (e.g. student.not_found ADR-056).
    private static IResult MapError(Error error) => error.Type switch
    {
        ErrorType.NotFound    => Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status404NotFound,            title: "Not Found"),
        ErrorType.Conflict    => Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status409Conflict,            title: "Conflict"),
        ErrorType.Validation  => Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status400BadRequest,          title: "Validation Error"),
        ErrorType.Unauthorized=> Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status401Unauthorized,        title: "Unauthorized"),
        ErrorType.Forbidden   => Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status403Forbidden,           title: "Forbidden"),
        _                     => Results.Problem(detail: $"[{error.Code}] {error.Message}", statusCode: StatusCodes.Status500InternalServerError,  title: "Internal Server Error")
    };
}

/// <summary>Request body for POST /api/payments/obligations.</summary>
public sealed record RegisterObligationRequest(
    string   StudentId,
    string   Concept,
    decimal  Amount,
    DateTime DueDate);

/// <summary>Request body for POST /api/payments/obligations/{id}/confirm.</summary>
public sealed record ConfirmPaymentRequest(
    string Method,
    string Reference);
