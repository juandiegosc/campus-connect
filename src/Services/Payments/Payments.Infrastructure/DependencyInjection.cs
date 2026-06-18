using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payments.Application.Abstractions;
using Payments.Infrastructure.Persistence;
using Payments.Infrastructure.Persistence.Repositories;
using Payments.Infrastructure.Services;

namespace Payments.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Payments infrastructure:
    /// - PaymentsDbContext (connection string key "PaymentsDb" with fallback "Default")
    /// - IUnitOfWork resolved from PaymentsDbContext
    /// - IObligationRepository
    /// - IUlidGenerator (singleton)
    /// - MassTransit EF Core outbox (NO consumers in Phase 1)
    /// CRITICAL (ADR-038): AddCampusConnectMassTransit is on IBusRegistrationConfigurator,
    /// NOT IServiceCollection — call INSIDE AddMassTransit delegate.
    /// </summary>
    public static IServiceCollection AddPaymentsInfrastructure(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        var connStr = configuration.GetConnectionString("PaymentsDb")
            ?? configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:PaymentsDb (or ConnectionStrings:Default) is required for Payments service.");

        services.AddDbContext<PaymentsDbContext>(opts => opts.UseNpgsql(connStr));

        // IUnitOfWork resolved from the same PaymentsDbContext instance
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PaymentsDbContext>());

        services.AddScoped<IObligationRepository, ObligationRepository>();

        // ULID generator is stateless — singleton is safe (ADR-036)
        services.AddSingleton<IUlidGenerator, UlidGenerator>();

        // CRITICAL (ADR-038): must be called inside AddMassTransit delegate
        // NO consumers in Phase 1 — AddCampusConnectMassTransit wires UseBusOutbox + UsePostgres
        services.AddMassTransit(cfg =>
            cfg.AddCampusConnectMassTransit<PaymentsDbContext>(configuration));

        return services;
    }
}
