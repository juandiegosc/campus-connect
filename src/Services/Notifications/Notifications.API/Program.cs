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
           .Enrich.WithProperty("Service", "notifications")
           .WriteTo.Console(new RenderedCompactJsonFormatter());
    });

    builder.Services.AddOpenApi();

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
