using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace BuildingBlocks.Infrastructure.OpenApi;

/// <summary>
/// Shared OpenAPI configuration for every CampusConnect 360 microservice.
///
/// Produces a richly-described OpenAPI 3.1 document (native .NET 10 generation — no Swashbuckle):
///   - Document-level API info (title, version, description, contact).
///   - A reusable "Bearer" HTTP/JWT security scheme registered in components.
///   - A per-operation security requirement applied to every endpoint EXCEPT those marked
///     [AllowAnonymous] (e.g. /health, login) so a front-end dev can see exactly which calls need a token.
///
/// The document is served at runtime via <c>MapOpenApi()</c> (/openapi/{documentName}.json) AND emitted to
/// a committed <c>Documentation/openapi.json</c> file at build time via Microsoft.Extensions.ApiDescription.Server.
/// </summary>
public static class OpenApiExtensions
{
    /// <param name="services">The application's service collection.</param>
    /// <param name="title">Human-readable API title, e.g. "CampusConnect 360 — Academic API".</param>
    /// <param name="description">Markdown-friendly summary of what the service does and how to authenticate.</param>
    /// <param name="version">Document version (default "v1").</param>
    public static IServiceCollection AddCampusConnectOpenApi(
        this IServiceCollection services,
        string title,
        string description,
        string version = "v1")
    {
        services.AddOpenApi(options =>
        {
            // Document-level API information (closure captures the per-service title/description).
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title       = title,
                    Version     = version,
                    Description = description,
                    Contact     = new OpenApiContact
                    {
                        Name = "CampusConnect 360 — Plataforma de gestión escolar"
                    }
                };
                return Task.CompletedTask;
            });

            // Registers the "Bearer" JWT scheme in components.securitySchemes (DI-activated — only if JWT is wired).
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();

            // Adds the Bearer requirement to each authenticated operation (skips [AllowAnonymous]).
            options.AddOperationTransformer<AuthorizationRequirementOperationTransformer>();
        });

        return services;
    }
}

/// <summary>
/// Adds the "Bearer" JWT security scheme to the document components, but only when a JWT bearer
/// authentication scheme is actually registered in the app (mirrors the Microsoft Learn sample for .NET 10).
/// Resolves <see cref="IAuthenticationSchemeProvider"/> OPTIONALLY from the document context so the
/// transformer is safe in services with no authentication wired (e.g. the Notifications/Analytics stubs).
/// </summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schemeProvider = context.ApplicationServices.GetService<IAuthenticationSchemeProvider>();
        if (schemeProvider is null)
            return; // no authentication configured (stub service) — nothing to document

        var schemes = await schemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(s => s.Name == "Bearer"))
            return;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type         = SecuritySchemeType.Http,
            Scheme       = "bearer",
            In           = ParameterLocation.Header,
            BearerFormat = "JWT",
            Description  = "JWT obtenido en POST /api/identity/auth/login. " +
                           "Enviar en el header: Authorization: Bearer {token}."
        };
    }
}

/// <summary>
/// Applies the "Bearer" security requirement to every operation that is NOT [AllowAnonymous].
/// This makes the lock icon / 401 contract explicit per endpoint so front-end devs know which calls need a token.
/// </summary>
internal sealed class AuthorizationRequirementOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        var isAnonymous = metadata.OfType<IAllowAnonymous>().Any();
        var requiresAuth = metadata.OfType<IAuthorizeData>().Any();

        if (isAnonymous || !requiresAuth || context.Document is null)
            return Task.CompletedTask;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        });

        return Task.CompletedTask;
    }
}
