using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payments.Application.Abstractions;
using Payments.Infrastructure.Messaging.Consumers;
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
    /// - IStudentReplicaRepository (Phase 2)
    /// - IUlidGenerator (singleton)
    /// - StudentEnrolledConsumer (Phase 2 — registered BEFORE AddCampusConnectMassTransit per ADR-042)
    /// CRITICAL (ADR-038): AddCampusConnectMassTransit is on IBusRegistrationConfigurator,
    /// NOT IServiceCollection — call INSIDE AddMassTransit delegate.
    /// CRITICAL (ADR-042): AddConsumer must come BEFORE AddCampusConnectMassTransit so
    /// topology configuration sees all consumers when wiring endpoints.
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

        // Phase 2: StudentReplica repository (ADR-054 — port purity, primitives/DTOs only)
        services.AddScoped<IStudentReplicaRepository, StudentReplicaRepository>();

        // ULID generator is stateless — singleton is safe (ADR-036)
        services.AddSingleton<IUlidGenerator, UlidGenerator>();

        // CRITICAL (ADR-038 + ADR-042): AddConsumer BEFORE AddCampusConnectMassTransit
        services.AddMassTransit(cfg =>
        {
            // Phase 2: StudentEnrolledConsumer — BEFORE topology wiring (ADR-042 R1)
            cfg.AddConsumer<StudentEnrolledConsumer>();

            // Phase 3: StudentStatusUpdatedConsumer — also BEFORE topology wiring (ADR-042)
            cfg.AddConsumer<StudentStatusUpdatedConsumer>();

            cfg.AddCampusConnectMassTransit<PaymentsDbContext>(configuration);
        });

        return services;
    }
}
