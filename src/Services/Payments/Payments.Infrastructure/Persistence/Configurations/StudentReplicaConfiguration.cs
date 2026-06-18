using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payments.Infrastructure.Persistence.ReadModels;

namespace Payments.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for StudentReplica read model.
/// Table: student_replicas. PK: student_id. Index: ix_student_replicas_grade (grade filter, ADR-059).
/// Picked up automatically by ApplyConfigurationsFromAssembly in PaymentsDbContext.
/// </summary>
public sealed class StudentReplicaConfiguration : IEntityTypeConfiguration<StudentReplica>
{
    public void Configure(EntityTypeBuilder<StudentReplica> b)
    {
        b.ToTable("student_replicas");

        b.HasKey(s => s.StudentId);

        b.Property(s => s.StudentId)
            .HasColumnName("student_id")
            .HasMaxLength(26)
            .ValueGeneratedNever();

        b.Property(s => s.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(200)
            .IsRequired();

        b.Property(s => s.Grade)
            .HasColumnName("grade")
            .HasMaxLength(50)
            .IsRequired();

        b.Property(s => s.SchoolId)
            .HasColumnName("school_id")
            .HasMaxLength(50)
            .IsRequired();

        b.Property(s => s.LastUpdatedAt)
            .HasColumnName("last_updated_at")
            .IsRequired();

        // Supports optional grade filter in GET /api/payments/students (ADR-059)
        b.HasIndex(s => s.Grade)
            .HasDatabaseName("ix_student_replicas_grade");
    }
}
