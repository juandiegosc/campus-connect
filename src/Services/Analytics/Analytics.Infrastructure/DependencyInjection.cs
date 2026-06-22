using Analytics.Application.Abstractions;
using Analytics.Infrastructure.Messaging.Consumers;
using Analytics.Infrastructure.Persistence;
using Analytics.Infrastructure.Persistence.Repositories;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Analytics.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Analytics infrastructure:
    /// - AnalyticsDbContext (connection string key "Default")
    /// - IUnitOfWork resolved from AnalyticsDbContext
    /// - IAnalyticsRepository
    /// - 7 projection consumers (StudentEnrolled, StudentStatusUpdated, PaymentConfirmed,
    ///   AttendanceRecorded, IncidentReported, NotificationSent, NotificationFailed)
    /// CRITICAL (ADR-042): AddConsumer must come BEFORE AddCampusConnectMassTransit.
    /// </summary>
    public static IServiceCollection AddAnalyticsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is required for Analytics service.");

        services.AddDbContext<AnalyticsDbContext>(opts => opts.UseNpgsql(connStr));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AnalyticsDbContext>());
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();

        services.AddMassTransit(cfg =>
        {
            cfg.AddConsumer<StudentEnrolledConsumer>();
            cfg.AddConsumer<StudentStatusUpdatedConsumer>();
            cfg.AddConsumer<PaymentConfirmedConsumer>();
            cfg.AddConsumer<AttendanceRecordedConsumer>();
            cfg.AddConsumer<IncidentReportedConsumer>();
            cfg.AddConsumer<NotificationSentConsumer>();
            cfg.AddConsumer<NotificationFailedConsumer>();

            cfg.AddCampusConnectMassTransit<AnalyticsDbContext>(configuration, "analytics"); // ADR-076: queue prefix
        });

        return services;
    }
}
