using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Analytics.API.Endpoints;
using Analytics.Application;
using Analytics.Infrastructure;
using Analytics.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.OpenApi;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;

// CRITICAL (Gotcha 20): Clear inbound claim type mapping BEFORE builder setup so 'role' is not remapped.
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateBootstrapLogger();

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
           .Enrich.WithProperty("Service", "analytics")
           .WriteTo.Console(new RenderedCompactJsonFormatter());
    });

    // Kernel infrastructure: TimeProvider, IIntegrationEventFactory, ICorrelationContext
    builder.Services.AddCampusConnectInfrastructure(builder.Configuration);

    // Analytics Application: MediatR + pipeline behaviors
    builder.Services.AddAnalyticsApplication();

    // Analytics Infrastructure: DbContext + MassTransit + projection consumers + repository
    builder.Services.AddAnalyticsInfrastructure(builder.Configuration);

    // JWT Bearer — identical config to the rest of the services (Gotcha 8)
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var signingKey = jwtSection["SigningKey"];
    if (string.IsNullOrWhiteSpace(signingKey))
        signingKey = "campus-connect-dev-placeholder-key-32b";

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.MapInboundClaims = false;
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
                RoleClaimType = "role",
                NameClaimType = "unique_name"
            };
        });

    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("Direccion", p => p.RequireRole("Direccion"));

    // NpgSql health check for analytics-db
    var connStr = builder.Configuration.GetConnectionString("Default")!;
    builder.Services.AddHealthChecks()
        .AddNpgSql(connStr, name: "analytics-db");

    builder.Services.AddCampusConnectOpenApi(
        title: "CampusConnect 360 — Analytics API",
        description:
            "Servicio de analítica (read model CQRS). " +
            "Consume eventos de negocio (StudentEnrolled, StudentStatusUpdated, PaymentConfirmed, " +
            "AttendanceRecorded, IncidentReported, NotificationSent, NotificationFailed) y construye " +
            "proyecciones para un tablero institucional con métricas agregadas. " +
            "Autenticación JWT Bearer.");

    var app = builder.Build();

    // Aplica migraciones EF al arrancar (no-op en build-time OpenAPI y en tests).
    app.MigrateDatabase<AnalyticsDbContext>();

    app.MapOpenApi();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapAnalyticsEndpoints();

    app.MapHealthChecks("/health");
    app.MapHealthChecks("/api/analytics/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Analytics service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// CRITICAL (Gotcha G5): public partial class Program for WebApplicationFactory<Program> in tests.
public partial class Program { }
