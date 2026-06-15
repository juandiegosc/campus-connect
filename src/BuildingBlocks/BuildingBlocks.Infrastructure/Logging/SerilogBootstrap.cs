using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Formatting.Compact;

namespace BuildingBlocks.Infrastructure.Logging;

public static class SerilogBootstrap
{
    /// <summary>
    /// Configures Serilog with configuration-driven settings, log context enrichment,
    /// a per-service "Service" property, and structured JSON output to console.
    /// </summary>
    public static WebApplicationBuilder AddCampusConnectLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((ctx, cfg) =>
        {
            cfg.ReadFrom.Configuration(ctx.Configuration)
               .Enrich.FromLogContext()
               .Enrich.WithProperty("Service", ctx.HostingEnvironment.ApplicationName)
               .WriteTo.Console(new RenderedCompactJsonFormatter());
        });

        return builder;
    }
}
