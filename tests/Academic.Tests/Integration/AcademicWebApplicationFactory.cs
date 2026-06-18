using Academic.Infrastructure.Persistence;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

namespace Academic.Tests.Integration;

/// <summary>
/// WebApplicationFactory that starts a real Postgres container via Testcontainers,
/// applies EF migrations, and replaces production MassTransit with InMemory TestHarness (ADR-033).
/// Shared across test classes via [Collection("AcademicPostgres")] to avoid per-test container cost.
/// </summary>
public sealed class AcademicWebApplicationFactory
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("academic_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync();

        // Apply migrations once — all tables including OutboxMessage, OutboxState, InboxState
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        await db.Database.MigrateAsync();

        // Ensure MassTransit test harness is started before any tests run
        var harness = Services.GetRequiredService<ITestHarness>();
        await harness.Start();
    }

    /// <inheritdoc />
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
                ["ConnectionStrings:AcademicDb"] = _pg.GetConnectionString(),
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
            // Target IBusControl + any IHostedService whose type name contains "MassTransit"
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
                d => d.ServiceType == typeof(DbContextOptions<AcademicDbContext>));
            if (optDesc is not null) services.Remove(optDesc);

            var ctxDesc = services.SingleOrDefault(
                d => d.ServiceType == typeof(AcademicDbContext));
            if (ctxDesc is not null) services.Remove(ctxDesc);

            // Re-register DbContext with container connection string
            services.AddDbContext<AcademicDbContext>(opts =>
                opts.UseNpgsql(_pg.GetConnectionString()));

            // Replace with InMemory TestHarness — keeps EF Core outbox active (ADR-033)
            services.AddMassTransitTestHarness(cfg =>
            {
                cfg.AddEntityFrameworkOutbox<AcademicDbContext>(o =>
                {
                    o.UsePostgres();
                    o.UseBusOutbox();
                });
                // No consumers in Phase 1
            });
        });
    }
}

/// <summary>
/// xUnit v3 collection definition — shares the Postgres container across all Academic integration tests.
/// </summary>
[CollectionDefinition("AcademicPostgres")]
public sealed class AcademicPostgresCollectionDefinition : ICollectionFixture<AcademicWebApplicationFactory>;
