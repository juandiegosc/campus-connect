using Attendance.Domain.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for AttendanceRecord aggregate.
/// Table: attendance_records. No FK to incidents (REQ-AT1-14 — aggregate independence).
/// DateOnly maps natively to Postgres 'date' via Npgsql 10 (ADR-074 — no converter).
/// </summary>
public sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> b)
    {
        b.ToTable("attendance_records");

        b.HasKey(r => r.Id);
        b.Property(r => r.Id)
            .HasColumnName("attendance_record_id")
            .HasMaxLength(26)
            .HasConversion(id => id.Value, val => AttendanceRecordId.FromRaw(val))
            .ValueGeneratedNever();

        b.Property(r => r.StudentId)
            .HasColumnName("student_id")
            .HasMaxLength(26)
            .IsRequired();

        // DateOnly native mapping via Npgsql 10 — NO converter (ADR-074)
        b.Property(r => r.Date)
            .HasColumnName("date")
            .IsRequired();

        // AttendanceStatus enum stored as string at DB boundary
        b.Property(r => r.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        b.Property(r => r.RecordedAt)
            .HasColumnName("recorded_at")
            .IsRequired();

        b.Property(r => r.SchoolId)
            .HasColumnName("school_id")
            .HasMaxLength(50)
            .IsRequired();

        b.HasIndex(r => r.StudentId)
            .HasDatabaseName("ix_attendance_records_student_id");

        // DomainEvents is a shadow property on AggregateRoot — NOT persisted
        b.Ignore(r => r.DomainEvents);
    }
}
