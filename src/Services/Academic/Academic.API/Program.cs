using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Academic.Application;
using Academic.Infrastructure;
using Academic.API.Endpoints;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.Correlation;
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
           .Enrich.WithProperty("Service", "academic")
           .WriteTo.Console(new RenderedCompactJsonFormatter());
    });

    // Kernel infrastructure: TimeProvider, ICorrelationContext
    builder.Services.AddCampusConnectInfrastructure(builder.Configuration);

    // Academic Application: MediatR + FluentValidation + pipeline behaviors
    builder.Services.AddAcademicApplication();

    // Academic Infrastructure: DbContext + MassTransit outbox + repositories
    builder.Services.AddAcademicInfrastructure(builder.Configuration);

    // JWT Bearer — identical config to Identity Phase 3 (ESC-34, R8)
    // IMPORTANT: SigningKey, Issuer, Audience MUST match Identity.API exactly.
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var signingKey = jwtSection["SigningKey"];
    if (string.IsNullOrWhiteSpace(signingKey))
    {
        // Fall back to dev placeholder key (local-only project — no external secret manager needed)
        // IMPORTANT (R8): must match Identity.API signing key — see design §ADR-037
        signingKey = "campus-connect-dev-placeholder-key-32b";
    }

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.MapInboundClaims = false;  // double defense (Gotcha 20)
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidIssuer              = jwtSection["Issuer"] ?? "campusconnect",
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

    // Authorization policies per ESC-35, ESC-36, ESC-37
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("Secretaria",          p => p.RequireRole("Secretaria"))
        .AddPolicy("SecretariaOrDireccion", p => p.RequireRole("Secretaria", "Direccion"));

    // NpgSql health check for academic-db (REQ-AC1-27)
    var connStr = builder.Configuration.GetConnectionString("AcademicDb")
                  ?? builder.Configuration.GetConnectionString("Default")!;
    builder.Services.AddHealthChecks()
        .AddNpgSql(connStr, name: "academic-db");

    builder.Services.AddOpenApi();

    var app = builder.Build();

    app.MapOpenApi();

    // Correlation ID middleware
    app.UseCampusConnectCorrelation();

    // CRITICAL (ESC-35): UseAuthentication BEFORE UseAuthorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Student endpoints (all 5)
    app.MapStudentEndpoints();

    // Health checks (pass through from stub + NpgSql check)
    app.MapHealthChecks("/health");
    app.MapGet("/api/academic/health", () => Results.Ok(new { status = "ok", service = "academic" }));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Academic service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// CRITICAL (ESC-41, G5): public partial class Program at end of file
// enables WebApplicationFactory<Program> in integration tests.
public partial class Program { }
