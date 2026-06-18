using Academic.Application.Abstractions;
using Academic.Infrastructure.Messaging;
using Academic.Infrastructure.Persistence;
using Academic.Infrastructure.Persistence.Repositories;
using Academic.Infrastructure.Services;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Academic.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Academic infrastructure services:
    /// - PostgreSQL DbContext (connection string key "AcademicDb")
    /// - IStudentRepository, IOutboxEventReader
    /// - IUlidGenerator (singleton)
    /// - MassTransit with EF Core outbox (ADR-038 — AddCampusConnectMassTransit is on IBusRegistrationConfigurator)
    /// NOTE: No consumers registered in Phase 1 — PaymentConfirmedConsumer deferred to Phase 2.
    /// </summary>
    public static IServiceCollection AddAcademicInfrastructure(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        // "AcademicDb" is the standard key; fall back to "Default" for backward compatibility
        // with docker-compose configurations that use the generic key name
        var connStr = configuration.GetConnectionString("AcademicDb")
            ?? configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:AcademicDb (or ConnectionStrings:Default) is required for Academic service.");

        services.AddDbContext<AcademicDbContext>(opts => opts.UseNpgsql(connStr));

        // IUnitOfWork resolved from the same AcademicDbContext instance
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AcademicDbContext>());

        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<IOutboxEventReader, OutboxEventReader>();

        // ULID generator is stateless — singleton is safe (ADR-036)
        services.AddSingleton<IUlidGenerator, UlidGenerator>();

        // CRITICAL (ADR-038): AddCampusConnectMassTransit is an extension on IBusRegistrationConfigurator,
        // NOT IServiceCollection. It MUST be called inside the AddMassTransit delegate.
        // Wrong pattern: services.AddCampusConnectMassTransit<AcademicDbContext>(configuration) — COMPILE ERROR
        services.AddMassTransit(cfg =>
            cfg.AddCampusConnectMassTransit<AcademicDbContext>(configuration));
        // No consumers in Phase 1 — PaymentConfirmedConsumer registered in Phase 2

        // Application port → MassTransit-backed publisher (keeps Application free of transport deps)
        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();

        return services;
    }
}
