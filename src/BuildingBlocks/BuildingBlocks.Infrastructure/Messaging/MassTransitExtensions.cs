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
    /// Queue naming convention: kebab-case PREFIXED by service name, e.g. for
    /// <paramref name="serviceName"/> = "attendance" a StudentEnrolledConsumer
    /// maps to queue "attendance-student-enrolled".
    ///
    /// CRITICAL (ADR-076): the per-service prefix prevents queue-name collisions
    /// between services hosting a same-named consumer. Without it, Payments and
    /// Attendance (both with StudentEnrolledConsumer) would share ONE queue named
    /// "StudentEnrolled" and become competing consumers — each StudentEnrolled
    /// event would reach only ONE service, leaving both student replicas incomplete.
    /// </summary>
    /// <typeparam name="TContext">EF Core DbContext that backs the transactional outbox.</typeparam>
    /// <param name="cfg">The MassTransit bus registration configurator.</param>
    /// <param name="configuration">Application configuration (RabbitMQ host/user/pass keys).</param>
    /// <param name="serviceName">Lowercase service identifier used as the queue prefix (e.g. "academic", "payments", "attendance").</param>
    public static void AddCampusConnectMassTransit<TContext>(
        this IBusRegistrationConfigurator cfg,
        IConfiguration configuration,
        string serviceName)
        where TContext : DbContext
    {
        // ADR-076: prefix every receive endpoint (queue) with the service name so
        // homonymous consumers in different services get distinct queues (real fan-out).
        cfg.SetEndpointNameFormatter(
            new KebabCaseEndpointNameFormatter(prefix: serviceName, includeNamespace: false));

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
