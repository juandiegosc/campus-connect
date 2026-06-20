using System.IdentityModel.Tokens.Jwt;
using System.Text;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.Correlation;
using BuildingBlocks.Infrastructure.OpenApi;
using Identity.API.Endpoints;
using BuildingBlocks.Infrastructure.Persistence;
using Identity.Application;
using Identity.Infrastructure;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateBootstrapLogger();

// CRITICAL (Gotcha G3): Clear inbound claim type mapping BEFORE any builder setup.
// Without this, 'sub' maps to ClaimTypes.NameIdentifier and 'role' maps to ClaimTypes.Role,
// breaking the /me endpoint claim extraction which reads original names: sub, unique_name, name, role.
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // En Docker cada contenedor usa 8080; en local se respeta el puerto de launchSettings.
    if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
        builder.WebHost.UseUrls("http://0.0.0.0:8080");

    builder.Host.UseSerilog((ctx, cfg) =>
    {
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .Enrich.FromLogContext()
           .Enrich.WithProperty("Service", "identity")
           .WriteTo.Console(new RenderedCompactJsonFormatter());
    });

    // Kernel infrastructure: TimeProvider, ICorrelationContext, IIntegrationEventFactory
    builder.Services.AddCampusConnectInfrastructure(builder.Configuration);

    // Kernel pipeline behaviors (Logging → Validation → UnitOfWork) + Identity Application
    // handlers + FluentValidation validators (all assemblies registered once via MediatR)
    builder.Services.AddCampusConnectApplication(
        typeof(BuildingBlocks.Application.DependencyInjection).Assembly,
        typeof(Identity.Application.DependencyInjection).Assembly);

    // Identity Infrastructure: PostgreSQL DbContext, UserRepository, BCrypt hasher, JWT service (ADR-019: no MassTransit)
    builder.Services.AddIdentityInfrastructure(builder.Configuration);

    // JWT Bearer authentication — validates tokens issued by this service (ESC-53).
    // IMPORTANT (R1): SigningKey, Issuer, and Audience MUST match the Gateway configuration.
    // Default values below are identical to src/Gateway/CampusConnect.Gateway/appsettings.json.
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var signingKey = jwtSection["SigningKey"];
    if (string.IsNullOrWhiteSpace(signingKey))
    {
        // Fall back to the default dev placeholder key when the env var is not set.
        // This is intentional for local-only deployments (constraint: campus-connect/execution-environment).
        // IMPORTANT (R1): must match the Gateway signing key — see design §6.1.
        signingKey = "campus-connect-dev-placeholder-key-32b";
    }

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.MapInboundClaims = false; // double defense against claim type mapping (Gotcha G3)
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSection["Issuer"] ?? "campusconnect",
                ValidateAudience = true,
                ValidAudience = jwtSection["Audience"] ?? "campusconnect-clients",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                // CRITICAL (Gotcha G3 cont.): Since DefaultInboundClaimTypeMap is cleared and
                // MapInboundClaims=false, the role claim stays as "role" (not ClaimTypes.Role URI).
                // We must tell the authorization system to look for plain "role" as the role claim.
                RoleClaimType = "role",
                NameClaimType = "unique_name"
            };
        });

    // Authorization policies
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("Direccion", p => p.RequireRole("Direccion"));

    // OpenAPI document (native .NET 10) — info + Bearer security scheme + per-endpoint auth requirements.
    // Served at /openapi/v1.json (runtime) and emitted to Documentation/openapi.json (build time).
    builder.Services.AddCampusConnectOpenApi(
        title: "CampusConnect 360 — Identity API",
        description:
            "Autenticación y gestión de usuarios: login con JWT Bearer y refresh token rotation (rotación single-use), " +
            "consulta del perfil del usuario autenticado vía /me (sin base de datos, desde claims del token), " +
            "y alta de usuarios restringida al rol Direccion. " +
            "Roles del sistema: Direccion, Secretaria, Finanzas, Docente.");

    // Configure JSON to accept enum values as strings (e.g., "Direccion" instead of 3)
    builder.Services.ConfigureHttpJsonOptions(opts =>
    {
        opts.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

    // Npgsql health check for identity-db (ESC-25, ESC-26)
    builder.Services.AddHealthChecks()
        .AddNpgSql(
            connectionStringFactory: sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")!,
            name: "identity-db",
            tags: ["ready"]);

    var app = builder.Build();

    // Aplica migraciones EF y siembra datos iniciales al arrancar (no-op en build-time OpenAPI y en tests).
    app.MigrateDatabase<IdentityDbContext>()
       .SeedDatabase(IdentityDbInitializer.Seed);

    // Correlation ID middleware — propagates X-Correlation-Id through the request chain
    app.UseCampusConnectCorrelation();

    // Authentication MUST come before Authorization (order is critical — ASP.NET Core middleware pipeline)
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapOpenApi();

    // Health endpoints — /health is the primary (used by docker HEALTHCHECK and Gateway)
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/api/identity/health");

    // Identity user endpoints: POST /api/identity/users (now requires Direccion role — Phase 3 breaking change)
    app.MapUserEndpoints();

    // Auth endpoints: POST /api/identity/auth/login, POST /api/identity/auth/refresh (public)
    app.MapAuthEndpoints();

    // Me endpoint: GET /api/identity/users/me (requires Bearer JWT)
    app.MapMeEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Identity service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// CRITICAL (ESC-54, Gotcha G4): partial class Program at the end of Program.cs
// enables WebApplicationFactory<Program> in integration tests.
// Must be public to allow test classes inheriting WebApplicationFactory<Program> to also be public
// (xUnit requires test classes to be public — xUnit1000).
public partial class Program { }
