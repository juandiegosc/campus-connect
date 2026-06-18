using Identity.Application.Users.GetCurrentUser;
using MediatR;

namespace Identity.API.Endpoints;

/// <summary>
/// Minimal API endpoint for the current user profile.
/// Reads from JWT claims — ZERO database roundtrip (ADR-028, ESC-46, ESC-60–61).
/// IHttpContextAccessor is NOT used — claims extracted directly from HttpContext.User.
/// </summary>
internal static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity/users").WithTags("Identity.Users");

        // GET /api/identity/users/me — requires valid Bearer JWT (ESC-60–61, REQ-P3-13)
        group.MapGet("/me", async (
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var user = httpContext.User;

            // Extract claims using ORIGINAL names (requires JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear()
            // and opts.MapInboundClaims = false in AddJwtBearer — see Program.cs, Gotcha G3).
            var sub      = user.FindFirst("sub")?.Value;
            var username = user.FindFirst("unique_name")?.Value;
            var fullName = user.FindFirst("name")?.Value;
            var role     = user.FindFirst("role")?.Value;

            if (sub is null || username is null || fullName is null || role is null)
                return Results.Unauthorized();

            if (!Guid.TryParse(sub, out var userId))
                return Results.Unauthorized();

            var query = new GetCurrentUserQuery(userId, username, fullName, role);
            var result = await sender.Send(query, ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Unauthorized();
        })
        .RequireAuthorization()
        .WithName("GetCurrentUser")
        .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
