using System.IdentityModel.Tokens.Jwt;
using System.Text;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.OpenApi;
using BuildingBlocks.Infrastructure.Persistence;
using Payments.Application;
using Payments.Infrastructure;
using Payments.Infrastructure.Persistence;
using Payments.API.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;

// CRITICAL (Gotcha 20): Clear inbound claim type mapping BEFORE builder setup.
// Without this, 'role' maps to ClaimTypes.Role URI, breaking policy evaluation.
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
           .Enrich.WithProperty("Service", "payments")
           .WriteTo.Console(new RenderedCompactJsonFormatter());
    });

    // Kernel infrastructure: TimeProvider, IIntegrationEventFactory, ICorrelationContext
    builder.Services.AddCampusConnectInfrastructure(builder.Configuration);

    // Payments Application: MediatR + FluentValidation + pipeline behaviors
    builder.Services.AddPaymentsApplication();

    // Payments Infrastructure: DbContext + MassTransit outbox + repositories
    builder.Services.AddPaymentsInfrastructure(builder.Configuration);

    // JWT Bearer — identical config to Identity.API and Academic.API (Gotcha 8)
    // IMPORTANT: SigningKey, Issuer, Audience MUST match Identity.API exactly.
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var signingKey  = jwtSection["SigningKey"];
    if (string.IsNullOrWhiteSpace(signingKey))
    {
        // Fall back to dev placeholder key (local-only project — Gotcha 8)
        signingKey = "campus-connect-dev-placeholder-key-32b";
    }

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.MapInboundClaims = false;  // double defense (Gotcha 20)
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidIssuer              = jwtSection["Issuer"]   ?? "campusconnect",
                ValidateAudience         = true,
                ValidAudience            = jwtSection["Audience"] ?? "campusconnect-clients",
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.FromSeconds(30),
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                RoleClaimType            = "role",
                NameClaimType            = "unique_name"
            };
        });

    // Authorization: Finanzas policy guards all 4 obligation endpoints (REQ-PM1-15)
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("Finanzas", p => p.RequireRole("Finanzas"));

    // NpgSql health check for payments-db (REQ-PM1-15, ESC-PM-30)
    var connStr = builder.Configuration.GetConnectionString("PaymentsDb")
                  ?? builder.Configuration.GetConnectionString("Default")!;
    builder.Services.AddHealthChecks()
        .AddNpgSql(connStr, name: "payments-db");

    // OpenAPI document (native .NET 10) — info + Bearer security scheme + per-endpoint auth requirements.
    // Served at /openapi/v1.json (runtime) and emitted to Documentation/openapi.json (build time).
    builder.Services.AddCampusConnectOpenApi(
        title: "CampusConnect 360 — Payments API",
        description:
            "Gestión de obligaciones de pago y confirmación de pagos para el módulo de Finanzas. " +
            "Permite registrar obligaciones de pago por estudiante, confirmar el pago (publica el evento `PaymentConfirmed`), " +
            "listar y consultar obligaciones por estado, y consultar la réplica local de estudiantes " +
            "(sincronizada vía eventos `StudentEnrolled` y `StudentStatusUpdated`). " +
            "Autenticación JWT Bearer requerida. Rol: **Finanzas**.");

    var app = builder.Build();

    // Aplica migraciones EF al arrancar (no-op en build-time OpenAPI y en tests).
    app.MigrateDatabase<PaymentsDbContext>();

    app.MapOpenApi();

    // CRITICAL (ESC-35): UseAuthentication BEFORE UseAuthorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Obligation endpoints (all 4)
    app.MapPaymentEndpoints();

    // Student replica endpoints — GET /api/payments/students (Phase 2, REQ-PM2-06)
    app.MapStudentEndpoints();

    // Health checks — both routes must return 200 (ESC-PM-30, REQ-PM1-15)
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/api/payments/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Payments service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// CRITICAL (Gotcha G5): public partial class Program at end of file
// enables WebApplicationFactory<Program> in integration tests.
public partial class Program { }
