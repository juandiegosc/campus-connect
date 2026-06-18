using Academic.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Academic.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Student aggregate root.
/// Applies snake_case column naming, owned-entity mappings, and value object conversions.
/// </summary>
public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("students");

        // Primary key — StudentId VO stored as VARCHAR(26), no value generation (handler provides ULID)
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("student_id")
            .HasMaxLength(26)
            .HasConversion(id => id.Value, val => StudentId.Parse(val))
            .ValueGeneratedNever();

        builder.Property(s => s.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(120)
            .IsRequired();

        // DocumentId VO — stored as VARCHAR(15), unique index
        builder.Property(s => s.DocumentId)
            .HasColumnName("document_id")
            .HasMaxLength(15)
            .IsRequired()
            .HasConversion(
                d => d.Value,
                v => DocumentId.Parse(v));

        builder.HasIndex(s => s.DocumentId)
            .HasDatabaseName("ix_students_document_id")
            .IsUnique();

        builder.Property(s => s.Grade)
            .HasColumnName("grade")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.SchoolId)
            .HasColumnName("school_id")
            .HasMaxLength(50)
            .IsRequired();

        // AcademicStatus enum — stored as VARCHAR(20) string
        builder.Property(s => s.AcademicStatus)
            .HasColumnName("academic_status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        // FinancialStatus enum — stored as VARCHAR(20) string
        builder.Property(s => s.FinancialStatus)
            .HasColumnName("financial_status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Enrollment owned entity (flat columns in the same table)
        builder.OwnsOne(s => s.Enrollment, enrollment =>
        {
            enrollment.Property(e => e.EnrollmentId)
                .HasColumnName("enrollment_id")
                .HasMaxLength(26)
                .IsRequired();

            enrollment.Property(e => e.CreatedAt)
                .HasColumnName("enrollment_created_at")
                .IsRequired();
        });

        // GuardianContact owned entity (flat columns in the same table)
        builder.OwnsOne(s => s.Guardian, guardian =>
        {
            guardian.Property(g => g.Name)
                .HasColumnName("guardian_name")
                .HasMaxLength(120)
                .IsRequired();

            guardian.Property(g => g.Email)
                .HasColumnName("guardian_email")
                .HasMaxLength(254)
                .IsRequired();
        });

        // DomainEvents is a shadow property on AggregateRoot — NOT persisted
        builder.Ignore(s => s.DomainEvents);
    }
}
