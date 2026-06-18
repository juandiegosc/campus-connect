using Academic.Domain.Students;
using BuildingBlocks.Infrastructure.Persistence;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Academic.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Academic bounded context.
/// Inherits domain-event dispatch and IUnitOfWork from <see cref="BaseDbContext"/>.
/// Registers all three MassTransit outbox tables (REQUIRED — ADR-035, G3).
/// Connection string key: "AcademicDb" (NOT "Default" — each service owns its key).
/// </summary>
public sealed class AcademicDbContext : BaseDbContext
{
    public DbSet<Student> Students => Set<Student>();

    public AcademicDbContext(DbContextOptions<AcademicDbContext> options, IPublisher publisher)
        : base(options, publisher)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AcademicDbContext).Assembly);

        // CRITICAL (G3): All THREE outbox tables MUST be registered.
        // Missing any one causes silent runtime failure — MassTransit fails at startup.
        // ESC-30 integration test verifies outbox_message table exists after MigrateAsync.
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddInboxStateEntity();
    }
}
