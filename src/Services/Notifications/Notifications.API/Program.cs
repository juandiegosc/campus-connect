using BuildingBlocks.Infrastructure.OpenApi;
using Serilog;
using Serilog.Formatting.Compact;

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

    builder.Services.AddCampusConnectOpenApi(
        title: "CampusConnect 360 — Notifications API",
        description:
            "Servicio de notificaciones (store-and-forward). STUB: aún sin dominio implementado — " +
            "expone solo health checks. Consumirá eventos (StudentEnrolled, PaymentConfirmed) para enviar " +
            "notificaciones en una fase futura.");

    var app = builder.Build();

    app.MapOpenApi();

    app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "notifications" }));
    app.MapGet("/api/notifications/health", () => Results.Ok(new { status = "ok", service = "notifications" }));

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
