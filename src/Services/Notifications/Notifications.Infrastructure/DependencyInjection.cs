using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Abstractions;
using Notifications.Infrastructure.Messaging.Consumers;
using Notifications.Infrastructure.Persistence;
using Notifications.Infrastructure.Persistence.Repositories;

namespace Notifications.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Notifications infrastructure:
    /// - NotificationsDbContext (connection string key "Default")
    /// - IUnitOfWork resolved from NotificationsDbContext
    /// - INotificationRepository
    /// - 5 consumers (4 Pub/Sub event consumers + 1 Point-to-Point command consumer)
    /// CRITICAL (ADR-042): AddConsumer must come BEFORE AddCampusConnectMassTransit so
    /// topology configuration sees all consumers when wiring endpoints.
    /// </summary>
    public static IServiceCollection AddNotificationsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is required for Notifications service.");

        services.AddDbContext<NotificationsDbContext>(opts => opts.UseNpgsql(connStr));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<NotificationsDbContext>());
        services.AddScoped<INotificationRepository, NotificationRepository>();

        services.AddMassTransit(cfg =>
        {
            cfg.AddConsumer<StudentEnrolledConsumer>();
            cfg.AddConsumer<PaymentConfirmedConsumer>();
            cfg.AddConsumer<AttendanceRecordedConsumer>();
            cfg.AddConsumer<IncidentReportedConsumer>();
            cfg.AddConsumer<SendNotificationConsumer>();

            cfg.AddCampusConnectMassTransit<NotificationsDbContext>(configuration, "notifications"); // ADR-076: queue prefix
        });

        return services;
    }
}
