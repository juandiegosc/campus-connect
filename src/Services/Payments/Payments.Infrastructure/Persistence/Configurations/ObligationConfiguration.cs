using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payments.Domain.Obligations;

namespace Payments.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Obligation aggregate root.
/// snake_case column names per design §5.1.
/// Owned Payment entity flat-mapped with nullable columns (null until confirmed).
/// </summary>
public sealed class ObligationConfiguration : IEntityTypeConfiguration<Obligation>
{
    public void Configure(EntityTypeBuilder<Obligation> builder)
    {
        builder.ToTable("obligations");

        // PK — ObligationId VO → VARCHAR(26), no value generation (handler provides ULID)
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasColumnName("obligation_id")
            .HasMaxLength(26)
            .HasConversion(id => id.Value, val => ObligationId.Parse(val))
            .ValueGeneratedNever();

        builder.Property(o => o.StudentId)
            .HasColumnName("student_id")
            .HasMaxLength(26)
            .IsRequired();

        builder.Property(o => o.Concept)
            .HasColumnName("concept")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(o => o.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(o => o.DueDate)
            .HasColumnName("due_date")
            .IsRequired();

        builder.Property(o => o.SchoolId)
            .HasColumnName("school_id")
            .HasMaxLength(50)
            .IsRequired();

        // ObligationStatus enum → stored as VARCHAR(20) string
        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(o => o.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Payment owned entity — flat columns, all nullable (null until confirmed — ADR-044)
        builder.OwnsOne(o => o.Payment, payment =>
        {
            payment.Property(p => p.Id)
                .HasColumnName("payment_id")
                .HasMaxLength(26)
                .HasConversion(id => id.Value, val => PaymentId.Parse(val));

            payment.Property(p => p.Method)
                .HasColumnName("payment_method")
                .HasMaxLength(20)
                .HasConversion<string>();

            payment.Property(p => p.Reference)
                .HasColumnName("payment_reference")
                .HasMaxLength(100);

            payment.Property(p => p.ConfirmedAt)
                .HasColumnName("payment_confirmed_at");
        });

        // Indexes for common query patterns
        builder.HasIndex(o => o.StudentId)
            .HasDatabaseName("ix_obligations_student_id");

        builder.HasIndex(o => o.Status)
            .HasDatabaseName("ix_obligations_status");

        // DomainEvents is a shadow property on AggregateRoot — NOT persisted
        builder.Ignore(o => o.DomainEvents);
    }
}
