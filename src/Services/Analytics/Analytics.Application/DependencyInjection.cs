using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Analytics.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers MediatR with the Analytics.Application assembly (query handlers + validators)
    /// and the shared MediatR pipeline behaviors from BuildingBlocks.Application.
    /// </summary>
    public static IServiceCollection AddAnalyticsApplication(this IServiceCollection services)
    {
        services.AddCampusConnectApplication(
            typeof(BuildingBlocks.Application.DependencyInjection).Assembly,
            typeof(DependencyInjection).Assembly);

        return services;
    }
}
