using BuildingBlocks.Application.Behaviors;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers MediatR, FluentValidation validators, and the three pipeline behaviors
    /// in order: Logging → Validation → UnitOfWork.
    /// </summary>
    public static IServiceCollection AddCampusConnectApplication(
        this IServiceCollection services,
        params System.Reflection.Assembly[] assemblies)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(assemblies);

            // Order matters: Logging → Validation → UnitOfWork
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(UnitOfWorkBehavior<,>));
        });

        services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);

        return services;
    }
}
