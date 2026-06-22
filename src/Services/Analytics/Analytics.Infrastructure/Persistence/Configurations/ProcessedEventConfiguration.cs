using Analytics.Domain.Projections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Analytics.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for ProcessedEvent. Table: processed_events. PK: event_id.</summary>
public sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> b)
    {
        b.ToTable("processed_events");

        b.HasKey(e => e.EventId);

        b.Property(e => e.EventId)
            .HasColumnName("event_id")
            .ValueGeneratedNever();

        b.Property(e => e.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(100)
            .IsRequired();

        b.Property(e => e.EntityId)
            .HasColumnName("entity_id")
            .HasMaxLength(100);

        b.Property(e => e.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(100);

        b.Property(e => e.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        b.Property(e => e.ReceivedAt)
            .HasColumnName("received_at")
            .IsRequired();

        b.HasIndex(e => e.EventType);
        b.HasIndex(e => e.ReceivedAt);
    }
}
