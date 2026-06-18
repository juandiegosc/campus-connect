using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <see cref="IdentityDbContext"/>.
/// Enables <c>dotnet ef migrations add</c> without a running Identity.API host (ADR-021, ESC-18).
/// Reads the connection string from the <c>ConnectionStrings__Default</c> environment variable,
/// or falls back to a local Postgres default for developer workstations.
/// </summary>
internal sealed class IdentityDbContextDesignFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    /// <inheritdoc />
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connStr = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                      ?? "Host=localhost;Port=5432;Database=identity_db;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connStr)
            .Options;

        // IPublisher in design-time context: stub no-op so EF CLI does not need MediatR DI.
        return new IdentityDbContext(options, new NoOpPublisher());
    }

    /// <summary>
    /// MediatR IPublisher stub for design-time use only.
    /// Both overloads are required to satisfy the interface (ADR-021).
    /// </summary>
    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }
}
