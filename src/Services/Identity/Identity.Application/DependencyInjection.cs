using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Application;

/// <summary>
/// DI extension for the Identity Application layer.
/// Registers MediatR handlers and FluentValidation validators from this assembly.
/// NOTE: Pipeline behaviors (Logging, Validation, UnitOfWork) are NOT registered here —
/// they are registered by <c>AddCampusConnectInfrastructure</c> via the kernel's
/// <c>AddCampusConnectApplication</c> call in Program.cs.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Identity Application services: MediatR handlers + FluentValidation validators.
    /// </summary>
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        return services;
    }
}
