using BuildingBlocks.Infrastructure.Persistence;
using Identity.Domain.RefreshTokens;
using Identity.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Identity bounded context.
/// Inherits domain-event dispatch and IUnitOfWork from <see cref="BaseDbContext"/> (ADR-023).
/// Does NOT register MassTransit Outbox/Inbox — Identity is MassTransit-free (ADR-019).
/// </summary>
public sealed class IdentityDbContext : BaseDbContext
{
    /// <summary>Users DbSet — mapped to the <c>users</c> table via <c>UserConfiguration</c>.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>RefreshTokens DbSet — mapped to the <c>refresh_tokens</c> table via <c>RefreshTokenConfiguration</c>.</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Initializes a new <see cref="IdentityDbContext"/>.
    /// </summary>
    /// <param name="options">EF Core options (connection string, provider, etc.).</param>
    /// <param name="publisher">MediatR publisher for domain event dispatch post-save.</param>
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options, IPublisher publisher)
        : base(options, publisher)
    {
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
