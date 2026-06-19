using BuildingBlocks.Application.Common;
using Identity.Application.Users.Login;
using Identity.Application.Users.Refresh;
using MediatR;

namespace Identity.API.Endpoints;

/// <summary>
/// Minimal API endpoint definitions for authentication flows.
/// Both endpoints are PUBLIC (no RequireAuthorization) — they are the entry points for obtaining tokens.
/// </summary>
internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity/auth").WithTags("Identity.Auth");

        // POST /api/identity/auth/login — public (ESC-55–57, REQ-P3-11)
        group.MapPost("/login", async (
            LoginRequest req,
            ISender sender,
            CancellationToken ct) =>
        {
            var cmd = new LoginCommand(req.Username, req.Password);
            var result = await sender.Send(cmd, ct);

            return result.Match(
                onSuccess: response => Results.Ok(response),
                onFailure: err => err.Type switch
                {
                    ErrorType.Unauthorized => Results.Unauthorized(),
                    ErrorType.Validation => Results.ValidationProblem(
                        new Dictionary<string, string[]>
                        {
                            ["error"] = [err.Message]
                        }),
                    _ => Results.Problem(detail: err.Message, statusCode: StatusCodes.Status500InternalServerError)
                });
        })
        .WithName("Login")
        .WithSummary("Iniciar sesión y obtener tokens JWT")
        .WithDescription(
            "Endpoint **público** (sin Bearer). Recibe `username` y `password`. " +
            "Devuelve un par de tokens: `accessToken` (JWT con claims sub, unique_name, name, role) " +
            "y `refreshToken` (GUID opaco, single-use). " +
            "401 si las credenciales son inválidas. 400 si el body no cumple la validación.")
        .Produces<LoginResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesValidationProblem();

        // POST /api/identity/auth/refresh — public (ESC-58–59, REQ-P3-12)
        group.MapPost("/refresh", async (
            RefreshRequest req,
            ISender sender,
            CancellationToken ct) =>
        {
            var cmd = new RefreshTokenCommand(req.RefreshToken);
            var result = await sender.Send(cmd, ct);

            return result.Match(
                onSuccess: response => Results.Ok(response),
                onFailure: err => err.Type switch
                {
                    ErrorType.Unauthorized => Results.Unauthorized(),
                    ErrorType.Validation => Results.ValidationProblem(
                        new Dictionary<string, string[]>
                        {
                            ["error"] = [err.Message]
                        }),
                    _ => Results.Problem(detail: err.Message, statusCode: StatusCodes.Status500InternalServerError)
                });
        })
        .WithName("RefreshToken")
        .WithSummary("Rotar refresh token y obtener nuevo par de tokens")
        .WithDescription(
            "Endpoint **público** (sin Bearer). Recibe el `refreshToken` opaco obtenido en login. " +
            "Revoca el token presentado y emite un nuevo `accessToken` + `refreshToken` (rotación single-use, ADR-027). " +
            "401 si el token no existe, fue revocado o expiró. 400 si el body no cumple la validación.")
        .Produces<LoginResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesValidationProblem();

        return app;
    }
}

/// <summary>Request body for <c>POST /api/identity/auth/login</c>.</summary>
public sealed record LoginRequest(string Username, string Password);

/// <summary>Request body for <c>POST /api/identity/auth/refresh</c>.</summary>
public sealed record RefreshRequest(string RefreshToken);
