using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Infrastructure.Messaging;

public static class MassTransitExtensions
{
    /// <summary>
    /// Configures MassTransit with RabbitMQ transport, retry policy, redelivery,
    /// DLQ (_error suffix), and EF Core Outbox for the given DbContext type.
    /// No consumers are registered here — each service wires its own consumers
    /// in its composition root.
    ///
    /// Queue/exchange naming convention: kebab-case (e.g. "student-enrolled").
    /// </summary>
    public static void AddCampusConnectMassTransit<TContext>(
        this IBusRegistrationConfigurator cfg,
        IConfiguration configuration)
        where TContext : DbContext
    {
        cfg.AddEntityFrameworkOutbox<TContext>(o =>
        {
            o.UsePostgres();
            o.UseBusOutbox();
        });

        cfg.UsingRabbitMq((context, mqCfg) =>
        {
            var host = configuration["RABBITMQ_HOST"] ?? "localhost";
            var user = configuration["RABBITMQ_USER"] ?? "guest";
            var pass = configuration["RABBITMQ_PASS"] ?? "guest";

            mqCfg.Host(host, "/", h =>
            {
                h.Username(user);
                h.Password(pass);
            });

            // Default retry: 3 attempts with incremental intervals
            mqCfg.UseMessageRetry(r => r.Incremental(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));

            // Scheduled redelivery (requires RabbitMQ delayed-message plugin or use InMemory scheduler)
            mqCfg.UseDelayedRedelivery(r => r.Intervals(
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(15)));

            // Dead-letter queue: MassTransit uses <queue-name>_error by convention
            // No additional config needed — MassTransit creates _error queues automatically.

            mqCfg.ConfigureEndpoints(context);
        });
    }
}
