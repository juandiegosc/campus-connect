using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Identity.Tests.Integration;

/// <summary>
/// WebApplicationFactory that starts a real Postgres container via Testcontainers
/// and applies EF Core migrations before tests run (ADR-029, ESC-54, REQ-P3-14).
/// Shared across a test collection to avoid container-per-test warm-up cost.
/// </summary>
public sealed class IdentityWebApplicationFactory
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("identity_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
        => await _container.StartAsync();

    /// <inheritdoc />
    public new async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Override configuration — replace DB connection string with container's and set known JWT values.
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"]  = _container.GetConnectionString(),
                ["Jwt:SigningKey"]             = "campus-connect-dev-placeholder-key-32b",
                ["Jwt:Issuer"]                 = "campusconnect",
                ["Jwt:Audience"]               = "campusconnect-clients",
                ["Jwt:AccessTokenMinutes"]     = "60",
                ["Jwt:RefreshTokenDays"]       = "7"
            });
        });

        // Override the DbContext registration to use the container's connection string.
        // Required because AddIdentityInfrastructure captures the connection string at service
        // registration time (before ConfigureWebHost overrides take effect in the test host build).
        builder.ConfigureTestServices(services =>
        {
            // Remove the existing DbContextOptions<IdentityDbContext> so AddDbContext registers fresh.
            var optionsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(Microsoft.EntityFrameworkCore.DbContextOptions<IdentityDbContext>));
            if (optionsDescriptor is not null)
                services.Remove(optionsDescriptor);

            // Also remove IdentityDbContext itself (the scoped registration added by AddDbContext).
            var ctxDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IdentityDbContext));
            if (ctxDescriptor is not null)
                services.Remove(ctxDescriptor);

            // Re-register with the container's connection string.
            services.AddDbContext<IdentityDbContext>(opts =>
                opts.UseNpgsql(_container.GetConnectionString()));
        });
    }

    /// <summary>
    /// Applies pending EF Core migrations to the test container database.
    /// Call once per test class in the constructor (idempotent — MigrateAsync skips applied migrations).
    /// </summary>
    public async Task ApplyMigrationsAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync();
    }
}

/// <summary>
/// xUnit v3 collection definition — shares the Postgres container across all tests in the "Postgres" collection.
/// </summary>
[CollectionDefinition("Postgres")]
public sealed class PostgresCollectionDefinition : ICollectionFixture<IdentityWebApplicationFactory>;
