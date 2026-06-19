using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Attendance.Application;
using Attendance.Infrastructure;
using Attendance.API.Endpoints;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.OpenApi;
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

    builder.WebHost.UseUrls("http://0.0.0.0:8080");

    builder.Host.UseSerilog((ctx, cfg) =>
    {
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .Enrich.FromLogContext()
           .Enrich.WithProperty("Service", "attendance")
           .WriteTo.Console(new RenderedCompactJsonFormatter());
    });

    // Kernel infrastructure: TimeProvider, IIntegrationEventFactory, ICorrelationContext
    builder.Services.AddCampusConnectInfrastructure(builder.Configuration);

    // Attendance Application: MediatR + FluentValidation + pipeline behaviors
    builder.Services.AddAttendanceApplication();

    // Attendance Infrastructure: DbContext + MassTransit outbox + repositories
    builder.Services.AddAttendanceInfrastructure(builder.Configuration);

    // JWT Bearer — identical config to Identity, Academic, Payments (Gotcha 8)
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

    // Authorization policies (REQ-AT1-34)
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("Docente", p => p.RequireRole("Docente"))
        .AddPolicy("DocenteOrDireccion", p => p.RequireRole("Docente", "Direccion"));

    // NpgSql health check for attendance-db (REQ-AT1-36)
    var connStr = builder.Configuration.GetConnectionString("AttendanceDb")
                  ?? builder.Configuration.GetConnectionString("Default")!;
    builder.Services.AddHealthChecks()
        .AddNpgSql(connStr, name: "attendance-db");

    // OpenAPI document (native .NET 10) — info + Bearer security scheme + per-endpoint auth requirements.
    // Served at /openapi/v1.json (runtime) and emitted to Documentation/openapi.json (build time).
    builder.Services.AddCampusConnectOpenApi(
        title: "CampusConnect 360 — Attendance API",
        description:
            "Registro de asistencia e incidentes/bienestar escolar. " +
            "Permite registrar asistencia diaria de estudiantes (publica AttendanceRecorded), " +
            "reportar incidentes de conducta o bienestar (publica IncidentReported), " +
            "consultar la réplica local de estudiantes sincronizada vía StudentEnrolled " +
            "y revisar el historial completo de asistencia e incidentes por estudiante. " +
            "Autenticación JWT Bearer (roles: Docente, Direccion).");

    var app = builder.Build();

    app.MapOpenApi();

    // CRITICAL: UseAuthentication BEFORE UseAuthorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Attendance endpoints (records, incidents, students, history)
    app.MapAttendanceEndpoints();

    // Health checks — both routes must return 200 (REQ-AT1-36)
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/api/attendance/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Attendance service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// CRITICAL (Gotcha G5): public partial class Program at end of file
// enables WebApplicationFactory<Program> in integration tests (REQ-AT1-47).
public partial class Program { }
