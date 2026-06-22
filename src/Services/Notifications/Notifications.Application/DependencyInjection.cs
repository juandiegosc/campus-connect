using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Notifications.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers MediatR with the Notifications.Application assembly (handlers + validators),
    /// FluentValidation validators, and the shared MediatR pipeline behaviors
    /// (Logging → Validation → UnitOfWork) from BuildingBlocks.Application.
    /// </summary>
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        services.AddCampusConnectApplication(
            typeof(BuildingBlocks.Application.DependencyInjection).Assembly,
            typeof(DependencyInjection).Assembly);

        return services;
    }
}
