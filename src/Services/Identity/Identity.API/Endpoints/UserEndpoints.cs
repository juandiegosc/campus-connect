using BuildingBlocks.Application.Common;
using Identity.Application.Users.RegisterUser;
using Identity.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Endpoints;

/// <summary>
/// Minimal API endpoint definitions for Identity user management.
/// </summary>
internal static class UserEndpoints
{
    /// <summary>
    /// Maps all user-related endpoints under <c>/api/identity/users</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        // PUBLIC endpoint — no [Authorize] required.
        // This service runs LOCAL-ONLY (constraint: campus-connect/execution-environment obs #155).
        // Phase 3 will add an admin role gate once JWT issuance is available.
        var group = app.MapGroup("/api/identity/users").WithTags("Identity.Users");

        group.MapPost("/", async (
            RegisterUserRequest req,
            ISender sender,
            CancellationToken ct) =>
        {
            var cmd = new RegisterUserCommand(req.Username, req.FullName, req.Password, req.Role);
            var result = await sender.Send(cmd, ct);

            return result.Match(
                onSuccess: id => Results.Created($"/api/identity/users/{id}", new { id }),
                onFailure: err => err.Type switch
                {
                    ErrorType.Conflict => Results.Conflict(new { code = err.Code, message = err.Message }),
                    ErrorType.Validation => Results.ValidationProblem(
                        new Dictionary<string, string[]>
                        {
                            ["error"] = [err.Message]
                        }),
                    _ => Results.Problem(
                        detail: err.Message,
                        statusCode: StatusCodes.Status500InternalServerError)
                });
        })
        // Phase 3 breaking change: endpoint now requires Direccion role (ESC-22, ESC-32, ESC-33).
        // Without a valid Bearer token with role=Direccion, requests return 401/403.
        // Bootstrap SQL: INSERT INTO users ... with role='Direccion' to seed the first admin.
        .RequireAuthorization("Direccion")
        .WithName("RegisterUser")
        .WithSummary("Crear un nuevo usuario del sistema")
        .WithDescription(
            "Requiere **Bearer JWT** con rol **Direccion**. " +
            "Crea un usuario con `username` único, `fullName`, `password` (mínimo 8 caracteres) y `role` " +
            "(Secretaria | Finanzas | Docente | Direccion). " +
            "Devuelve 201 con el `id` (GUID) del usuario creado. " +
            "409 si el `username` ya existe. 400 si los datos no superan la validación. " +
            "401 sin token; 403 si el rol no es Direccion.")
        .Produces<RegisterUserResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}

/// <summary>Response body for <c>POST /api/identity/users</c> (201 Created).</summary>
public sealed record RegisterUserResponse(Guid Id);

/// <summary>
/// Request body for <c>POST /api/identity/users</c>.
/// </summary>
/// <param name="Username">Unique username (alphanumeric + . _ -).</param>
/// <param name="FullName">Full display name.</param>
/// <param name="Password">Raw password (min 8 chars).</param>
/// <param name="Role">User role as string (Secretaria | Finanzas | Docente | Direccion).</param>
public sealed record RegisterUserRequest(
    string Username,
    string FullName,
    string Password,
    UserRole Role);
