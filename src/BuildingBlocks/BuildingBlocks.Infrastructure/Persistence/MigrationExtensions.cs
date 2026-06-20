using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Applies EF Core migrations on application startup. This is a LOCAL-ONLY project, so auto-migrating
/// at boot is acceptable (no production gating required) and makes both `dotnet run` and Docker work
/// against a fresh database without a manual `dotnet ef database update` step.
/// </summary>
public static class MigrationExtensions
{
    /// <summary>
    /// Applies pending migrations for <typeparamref name="TContext"/>. No-op when:
    ///   - running under build-time OpenAPI document generation (entry assembly "GetDocument.Insider" — no DB), or
    ///   - the host environment is "Testing" (integration tests own their Testcontainers migration).
    /// Retries a few times so the service tolerates a database that is still starting up.
    /// </summary>
    public static WebApplication MigrateDatabase<TContext>(this WebApplication app, int retries = 10)
        where TContext : DbContext
    {
        // Build-time OpenAPI generation mock-runs the entrypoint with no database available.
        if (Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider")
            return app;

        // Integration tests (WebApplicationFactory) migrate their own Testcontainers database.
        if (app.Environment.IsEnvironment("Testing"))
            return app;

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                db.Database.Migrate();
                return app;
            }
            catch when (attempt < retries)
            {
                // Database may still be starting (e.g. Docker). Wait and retry.
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }
    }
}
