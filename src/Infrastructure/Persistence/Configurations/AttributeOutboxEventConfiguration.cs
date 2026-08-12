using KartCategoryService.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KartCategoryService.Infrastructure.Persistence.Configurations;

/// <summary>Maps AttributeOutboxEvent to `attribute_outbox_events`, mirroring CategoryOutboxEventConfiguration.</summary>
public sealed class AttributeOutboxEventConfiguration : IEntityTypeConfiguration<AttributeOutboxEvent>
{
    public void Configure(EntityTypeBuilder<AttributeOutboxEvent> builder)
    {
        builder.ToTable("attribute_outbox_events");

        builder.HasKey(e => e.OutboxId);
        builder.Property(e => e.OutboxId).HasColumnName("outbox_id").ValueGeneratedNever();

        builder.Property(e => e.AttributeId).HasColumnName("attribute_id").IsRequired();
        builder.Property(e => e.EventType).HasColumnName("event_type").HasColumnType("text").IsRequired();
        builder.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(e => e.PublishedAt).HasColumnName("published_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasColumnType("text").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasColumnType("text").IsRequired();
        builder.Property(e => e.TraceParent).HasColumnName("trace_parent").HasColumnType("text");

        builder.HasOne<Domain.Attributes.ProductAttribute>()
            .WithMany()
            .HasForeignKey(e => e.AttributeId)
            .HasConstraintName("FK_attribute_outbox_events_attribute_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.OccurredAt)
            .HasDatabaseName("idx_attribute_outbox_unpublished")
            .HasFilter("published_at IS NULL");
    }
}
