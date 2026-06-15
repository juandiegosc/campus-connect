using BuildingBlocks.Application.Correlation;
using BuildingBlocks.Contracts.Abstractions;
using BuildingBlocks.Infrastructure.Correlation;
using BuildingBlocks.Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers core infrastructure services:
    /// - TimeProvider.System (Singleton)
    /// - IIntegrationEventFactory → IntegrationEventFactory (Scoped)
    /// - IHttpContextAccessor (required for correlation)
    /// - ICorrelationContext → HttpCorrelationContext (Scoped, ESC-05.3)
    /// </summary>
    public static IServiceCollection AddCampusConnectInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddHttpContextAccessor();

        services.AddScoped<ICorrelationContext, HttpCorrelationContext>();
        services.AddScoped<IIntegrationEventFactory, IntegrationEventFactory>();

        return services;
    }
}
