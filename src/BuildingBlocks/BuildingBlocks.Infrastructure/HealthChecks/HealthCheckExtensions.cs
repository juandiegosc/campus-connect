using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure.HealthChecks;

public static class HealthCheckExtensions
{
    /// <summary>
    /// Adds the base health checks builder. Returns IHealthChecksBuilder so individual
    /// services can chain additional probes (e.g., .AddNpgSql(...), .AddRabbitMQ(...)).
    /// </summary>
    public static IHealthChecksBuilder AddCampusConnectHealthChecks(this IServiceCollection services)
        => services.AddHealthChecks();
}
