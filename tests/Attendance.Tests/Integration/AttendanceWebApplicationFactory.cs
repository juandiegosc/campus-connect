using Attendance.Infrastructure.Messaging.Consumers;
using Attendance.Infrastructure.Persistence;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

namespace Attendance.Tests.Integration;

/// <summary>
/// WebApplicationFactory that starts a real Postgres container via Testcontainers,
/// applies EF migrations, and replaces production MassTransit with InMemory TestHarness (ADR-033).
/// Shared across test classes via [Collection("AttendancePostgres")] to avoid per-test container cost.
///
/// CRITICAL (ADR-042): StudentEnrolledConsumer registered in BOTH DependencyInjection.cs AND here.
/// CRITICAL: AddConsumer must come BEFORE AddEntityFrameworkOutbox (ADR-042 R3).
/// </summary>
public sealed class AttendanceWebApplicationFactory
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("attendance_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync();

        // Apply all migrations: attendance_records, incidents, student_replicas, OutboxMessage, OutboxState, InboxState
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
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
                ["ConnectionStrings:AttendanceDb"] = _pg.GetConnectionString(),
                ["Jwt:SigningKey"]                 = "campus-connect-dev-placeholder-key-32b",
                ["Jwt:Issuer"]                     = "campusconnect",
                ["Jwt:Audience"]                   = "campusconnect-clients",
                ["RABBITMQ_HOST"]                  = "localhost",
                ["RABBITMQ_USER"]                  = "guest",
                ["RABBITMQ_PASS"]                  = "guest"
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
                d => d.ServiceType == typeof(DbContextOptions<AttendanceDbContext>));
            if (optDesc is not null) services.Remove(optDesc);

            var ctxDesc = services.SingleOrDefault(
                d => d.ServiceType == typeof(AttendanceDbContext));
            if (ctxDesc is not null) services.Remove(ctxDesc);

            // Re-register DbContext with container connection string
            services.AddDbContext<AttendanceDbContext>(opts =>
                opts.UseNpgsql(_pg.GetConnectionString()));

            // Replace with InMemory TestHarness — keeps EF Core outbox active (ADR-033)
            // CRITICAL (ADR-042): StudentEnrolledConsumer BEFORE AddEntityFrameworkOutbox (R1 — order matters).
            services.AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<StudentEnrolledConsumer>();  // ADR-042 mirror — BEFORE outbox wiring

                cfg.AddEntityFrameworkOutbox<AttendanceDbContext>(o =>
                {
                    o.UsePostgres();
                    o.UseBusOutbox();
                });
            });
        });
    }
}

/// <summary>
/// xUnit v3 collection definition — shares the Postgres container across all Attendance integration tests.
/// </summary>
[CollectionDefinition("AttendancePostgres")]
public sealed class AttendancePostgresCollectionDefinition : ICollectionFixture<AttendanceWebApplicationFactory>;
