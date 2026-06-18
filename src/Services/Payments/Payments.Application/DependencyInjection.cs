using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Payments.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers MediatR with the Payments.Application assembly (handlers + validators),
    /// FluentValidation validators, and the shared MediatR pipeline behaviors
    /// (Logging → Validation → UnitOfWork) from BuildingBlocks.Application.
    /// </summary>
    public static IServiceCollection AddPaymentsApplication(this IServiceCollection services)
    {
        services.AddCampusConnectApplication(
            typeof(BuildingBlocks.Application.DependencyInjection).Assembly,
            typeof(DependencyInjection).Assembly);

        return services;
    }
}
