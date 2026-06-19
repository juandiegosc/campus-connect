using Attendance.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Incident aggregate.
/// Table: incidents. NO FK to attendance_records (REQ-AT1-14 — aggregate independence).
/// IncidentSeverity stored as string at DB boundary.
/// </summary>
public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> b)
    {
        b.ToTable("incidents");

        b.HasKey(i => i.Id);
        b.Property(i => i.Id)
            .HasColumnName("incident_id")
            .HasMaxLength(26)
            .HasConversion(id => id.Value, val => IncidentId.FromRaw(val))
            .ValueGeneratedNever();

        b.Property(i => i.StudentId)
            .HasColumnName("student_id")
            .HasMaxLength(26)
            .IsRequired();

        b.Property(i => i.Type)
            .HasColumnName("type")
            .IsRequired();

        // IncidentSeverity enum stored as string at DB boundary
        b.Property(i => i.Severity)
            .HasColumnName("severity")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        // Description stored locally, NOT published (REQ-AT1-13)
        b.Property(i => i.Description)
            .HasColumnName("description")
            .IsRequired();

        b.Property(i => i.ReportedAt)
            .HasColumnName("reported_at")
            .IsRequired();

        b.Property(i => i.SchoolId)
            .HasColumnName("school_id")
            .HasMaxLength(50)
            .IsRequired();

        b.HasIndex(i => i.StudentId)
            .HasDatabaseName("ix_incidents_student_id");

        // DomainEvents is a shadow property on AggregateRoot — NOT persisted
        b.Ignore(i => i.DomainEvents);
    }
}
