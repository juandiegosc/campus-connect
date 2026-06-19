using BuildingBlocks.Infrastructure.OpenApi;
using Serilog;
using Serilog.Formatting.Compact;

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
           .Enrich.WithProperty("Service", "analytics")
           .WriteTo.Console(new RenderedCompactJsonFormatter());
    });

    builder.Services.AddCampusConnectOpenApi(
        title: "CampusConnect 360 — Analytics API",
        description:
            "Servicio de analítica (proyecciones de lectura). STUB: aún sin dominio implementado — " +
            "expone solo health checks. Proyectará eventos del sistema (StudentEnrolled, PaymentConfirmed, " +
            "AttendanceRecorded, etc.) a modelos de lectura en una fase futura.");

    var app = builder.Build();

    app.MapOpenApi();

    app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "analytics" }));
    app.MapGet("/api/analytics/health", () => Results.Ok(new { status = "ok", service = "analytics" }));

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
