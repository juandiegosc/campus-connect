using Attendance.Domain.Attendance;
using Attendance.Domain.Incidents;
using Attendance.Infrastructure.Persistence.ReadModels;
using BuildingBlocks.Infrastructure.Persistence;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Attendance bounded context.
/// Inherits domain-event dispatch and IUnitOfWork from BaseDbContext.
/// Outbox tables: OutboxMessage + OutboxState.
/// InboxState: ACTIVE (StudentEnrolledConsumer present in Phase 1 — REQ-AT1-27).
/// StudentReplicas: read model fed by StudentEnrolledConsumer.
/// Connection string key: "AttendanceDb" (fallback "Default").
/// </summary>
public sealed class AttendanceDbContext : BaseDbContext
{
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Incident>         Incidents         => Set<Incident>();
    public DbSet<StudentReplica>   StudentReplicas   => Set<StudentReplica>();

    public AttendanceDbContext(DbContextOptions<AttendanceDbContext> options, IPublisher publisher)
        : base(options, publisher)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AttendanceDbContext).Assembly);

        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        // InboxState: active from Phase 1 (consumer present — REQ-AT1-27, ADR-055)
        modelBuilder.AddInboxStateEntity();
    }
}
