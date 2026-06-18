using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Academic.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers MediatR with the Academic.Application assembly (handlers + validators),
    /// FluentValidation validators, and the shared MediatR pipeline behaviors
    /// (Logging → Validation → UnitOfWork) from BuildingBlocks.Application.
    /// </summary>
    public static IServiceCollection AddAcademicApplication(this IServiceCollection services)
    {
        services.AddCampusConnectApplication(
            typeof(BuildingBlocks.Application.DependencyInjection).Assembly,
            typeof(DependencyInjection).Assembly);

        return services;
    }
}
