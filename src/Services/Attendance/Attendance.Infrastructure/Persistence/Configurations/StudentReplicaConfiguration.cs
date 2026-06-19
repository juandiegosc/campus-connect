using Attendance.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for StudentReplica read model.
/// Table: student_replicas. PK: student_id.
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
    }
}
