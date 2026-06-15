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
           .Enrich.WithProperty("Service", "identity")
           .WriteTo.Console(new RenderedCompactJsonFormatter());
    });

    builder.Services.AddOpenApi();

    var app = builder.Build();

    app.MapOpenApi();

    // Direct liveness probe (Docker healthcheck)
    app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "identity" }));

    // Gateway-forwarded probe (Ocelot forwards /api/identity/{everything} as-is)
    app.MapGet("/api/identity/health", () => Results.Ok(new { status = "ok", service = "identity" }));

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
