using System.IdentityModel.Tokens.Jwt;
using System.Text;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.OpenApi;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Notifications.API.Endpoints;
using Notifications.Application;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
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
           .Enrich.WithProperty("Service", "notifications")
           .WriteTo.Console(new RenderedCompactJsonFormatter());
    });

    // Kernel infrastructure: TimeProvider, IIntegrationEventFactory, ICorrelationContext
    builder.Services.AddCampusConnectInfrastructure(builder.Configuration);

    // Notifications Application: MediatR + FluentValidation + pipeline behaviors
    builder.Services.AddNotificationsApplication();

    // Notifications Infrastructure: DbContext + MassTransit outbox + consumers + repository
    builder.Services.AddNotificationsInfrastructure(builder.Configuration);

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

    // NpgSql health check for notifications-db
    var connStr = builder.Configuration.GetConnectionString("Default")!;
    builder.Services.AddHealthChecks()
        .AddNpgSql(connStr, name: "notifications-db");

    builder.Services.AddCampusConnectOpenApi(
        title: "CampusConnect 360 — Notifications API",
        description:
            "Servicio de notificaciones (envío simulado). " +
            "Consume eventos de negocio (StudentEnrolled, PaymentConfirmed, AttendanceRecorded, IncidentReported) " +
            "mediante Pub/Sub y genera notificaciones, publicando NotificationSent o NotificationFailed. " +
            "Expone además un endpoint Point-to-Point para encolar notificaciones ad-hoc. " +
            "Autenticación JWT Bearer.");

    var app = builder.Build();

    // Aplica migraciones EF al arrancar (no-op en build-time OpenAPI y en tests).
    app.MigrateDatabase<NotificationsDbContext>();

    app.MapOpenApi();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapNotificationsEndpoints();

    app.MapHealthChecks("/health");
    app.MapHealthChecks("/api/notifications/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Notifications service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// CRITICAL (Gotcha G5): public partial class Program for WebApplicationFactory<Program> in tests.
public partial class Program { }
