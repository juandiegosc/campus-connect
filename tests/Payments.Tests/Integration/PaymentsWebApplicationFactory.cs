using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Payments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Payments.Tests.Integration;

/// <summary>
/// WebApplicationFactory that starts a real Postgres container via Testcontainers,
/// applies EF migrations, and replaces production MassTransit with InMemory TestHarness (ADR-033).
/// Shared across test classes via [Collection("PaymentsPostgres")] to avoid per-test container cost.
///
/// IMPORTANT — NO InboxState (ADR-046): Payments Phase 1 has no consumers.
/// Only OutboxMessage + OutboxState tables are created by InitialPayments migration.
///
/// CRITICAL for PaymentConfirmed verification (Gotcha 28):
/// Use harness.Published.Any&lt;PaymentConfirmed&gt;() — NOT raw SQL COUNT on OutboxMessage.
/// UseBusOutbox() commits and removes the row from OutboxMessage within the test transaction.
/// </summary>
public sealed class PaymentsWebApplicationFactory
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("payments_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync();

        // Apply migrations once — obligations, OutboxMessage, OutboxState (NO InboxState — ADR-046)
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        await db.Database.MigrateAsync();

        // Ensure MassTransit test harness is started before any tests run
        var harness = Services.GetRequiredService<ITestHarness>();
        await harness.Start();
    }

    public new async ValueTask DisposeAsync()
    {
        await _pg.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Override configuration — point to test Postgres container and use known JWT constants
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PaymentsDb"] = _pg.GetConnectionString(),
                ["Jwt:SigningKey"]               = "campus-connect-dev-placeholder-key-32b",
                ["Jwt:Issuer"]                   = "campusconnect",
                ["Jwt:Audience"]                 = "campusconnect-clients",
                ["RABBITMQ_HOST"]                = "localhost",
                ["RABBITMQ_USER"]                = "guest",
                ["RABBITMQ_PASS"]                = "guest"
            }));

        builder.ConfigureTestServices(services =>
        {
            // Remove production MassTransit services (ADR-033, G7)
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(IBusControl) ||
                    (d.ServiceType == typeof(IHostedService) &&
                     d.ImplementationType?.FullName?.Contains("MassTransit") == true))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            // Remove existing DbContextOptions so we can re-register with container connection string
            var optDesc = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PaymentsDbContext>));
            if (optDesc is not null) services.Remove(optDesc);

            var ctxDesc = services.SingleOrDefault(
                d => d.ServiceType == typeof(PaymentsDbContext));
            if (ctxDesc is not null) services.Remove(ctxDesc);

            // Re-register DbContext with container connection string
            services.AddDbContext<PaymentsDbContext>(opts =>
                opts.UseNpgsql(_pg.GetConnectionString()));

            // Replace with InMemory TestHarness — keeps EF Core outbox active (ADR-033)
            // Phase 1: no consumers, so no AddConsumer calls here (ADR-046)
            services.AddMassTransitTestHarness(cfg =>
            {
                cfg.AddEntityFrameworkOutbox<PaymentsDbContext>(o =>
                {
                    o.UsePostgres();
                    o.UseBusOutbox();
                });
            });
        });
    }
}

/// <summary>
/// xUnit v3 collection definition — shares the Postgres container across all Payments integration tests.
/// </summary>
[CollectionDefinition("PaymentsPostgres")]
public sealed class PaymentsPostgresCollectionDefinition : ICollectionFixture<PaymentsWebApplicationFactory>;
