using Analytics.Domain.Projections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Analytics.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for StudentProjection. Table: student_projections. PK: student_id.</summary>
public sealed class StudentProjectionConfiguration : IEntityTypeConfiguration<StudentProjection>
{
    public void Configure(EntityTypeBuilder<StudentProjection> b)
    {
        b.ToTable("student_projections");

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

        b.Property(s => s.AcademicStatus)
            .HasColumnName("academic_status")
            .HasMaxLength(50)
            .IsRequired();

        b.Property(s => s.FinancialStatus)
            .HasColumnName("financial_status")
            .HasMaxLength(50)
            .IsRequired();

        b.Property(s => s.LastUpdatedAt)
            .HasColumnName("last_updated_at")
            .IsRequired();
    }
}
