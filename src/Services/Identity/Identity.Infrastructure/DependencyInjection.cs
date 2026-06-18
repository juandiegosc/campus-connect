using BuildingBlocks.Application.Abstractions;
using Identity.Application.Abstractions;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Repositories;
using Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.Infrastructure;

/// <summary>
/// DI extension for the Identity Infrastructure layer.
/// Identity is MassTransit-free (ADR-019) — DO NOT call AddCampusConnectMassTransit here.
/// Connection string key is "Default" (ADR-020).
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Identity infrastructure services:
    /// PostgreSQL DbContext (connection string key "Default" — ADR-020),
    /// UserRepository, BCrypt password hasher, and IUnitOfWork.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration (reads ConnectionStrings:Default).</param>
    /// <returns>The modified service collection.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the "Default" connection string is missing or empty (ADR-020).
    /// </exception>
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is required for Identity (ADR-020). " +
                "Set it via appsettings.json or the ConnectionStrings__Default environment variable.");

        services.AddDbContext<IdentityDbContext>(opts => opts.UseNpgsql(connStr));

        // IUnitOfWork resolved from the same IdentityDbContext instance (ADR-023).
        // No adapter class needed — IdentityDbContext inherits from BaseDbContext : IUnitOfWork.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());

        services.AddScoped<IUserRepository, UserRepository>();

        // BCrypt hasher is stateless — singleton is safe.
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        // TimeProvider — use TryAdd to avoid duplicate registration if already registered elsewhere.
        services.TryAddSingleton(TimeProvider.System);

        // JWT token service — stateless singleton (reads config once in constructor).
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // Refresh token repository — scoped (uses DbContext which is scoped).
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // NOTE: MassTransit is intentionally excluded (ADR-019). Identity publishes no
        // integration events in Phase 2. Future phases may introduce events via Outbox.

        return services;
    }
}
