using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain.Notifications;

namespace Notifications.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Notification aggregate. Table: notifications. PK: notification_id (ULID).
/// </summary>
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");

        b.HasKey(n => n.Id);

        b.Property(n => n.Id)
            .HasColumnName("notification_id")
            .HasMaxLength(26)
            .HasConversion(id => id.Value, raw => NotificationId.FromRaw(raw))
            .ValueGeneratedNever();

        b.Property(n => n.SourceEvent)
            .HasColumnName("source_event")
            .HasMaxLength(100)
            .IsRequired();

        b.Property(n => n.StudentId)
            .HasColumnName("student_id")
            .HasMaxLength(26);

        b.Property(n => n.Channel)
            .HasColumnName("channel")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        b.Property(n => n.Recipient)
            .HasColumnName("recipient")
            .HasMaxLength(200)
            .IsRequired();

        b.Property(n => n.Subject)
            .HasColumnName("subject")
            .HasMaxLength(200)
            .IsRequired();

        b.Property(n => n.Body)
            .HasColumnName("body")
            .HasMaxLength(2000)
            .IsRequired();

        b.Property(n => n.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        b.Property(n => n.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(500);

        b.Property(n => n.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        b.Property(n => n.SchoolId)
            .HasColumnName("school_id")
            .HasMaxLength(50)
            .IsRequired();

        b.HasIndex(n => n.CreatedAt);
    }
}
