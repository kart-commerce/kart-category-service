using System.Text.Json;
using KartCategoryService.Domain.Attributes;

namespace KartCategoryService.Infrastructure.Messaging;

/// <summary>
/// database-design.md `attribute_outbox_events` - one row per create/update/deprecate, written in
/// the same transaction as the `attributes` write it describes. Mirrors CategoryOutboxEvent's shape
/// exactly (including TraceParent from day one - unlike category_outbox_events, which needed a
/// later migration to add it).
/// </summary>
public sealed class AttributeOutboxEvent
{
    public const string AttributeUpdatedEventType = "AttributeUpdated";
    private const string RelaySystemPrincipal = "system:category-outbox-relay";

    private static readonly JsonSerializerOptions PayloadSerializerOptions = new(JsonSerializerDefaults.Web);

    public Guid OutboxId { get; private set; }
    public Guid AttributeId { get; private set; }
    public string EventType { get; private set; } = AttributeUpdatedEventType;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = RelaySystemPrincipal;
    public string? TraceParent { get; private set; }

    private AttributeOutboxEvent()
    {
    }

    public static AttributeOutboxEvent FromDomainEvent(AttributeUpdatedDomainEvent domainEvent, string actingPrincipal, string? traceParent)
    {
        var payload = AttributeUpdatedEventPayload.FromDomainEvent(domainEvent);

        return new AttributeOutboxEvent
        {
            OutboxId = Guid.NewGuid(),
            AttributeId = domainEvent.AttributeId,
            EventType = AttributeUpdatedEventType,
            Payload = JsonSerializer.Serialize(payload, PayloadSerializerOptions),
            OccurredAt = domainEvent.OccurredAt,
            CreatedBy = actingPrincipal,
            UpdatedBy = RelaySystemPrincipal,
            TraceParent = traceParent,
        };
    }

    public void MarkPublished(DateTimeOffset publishedAt)
    {
        PublishedAt = publishedAt;
    }

    public static implicit operator OutboxEventRecord(AttributeOutboxEvent e) => new(
        e.OutboxId, e.EventType, e.Payload, e.OccurredAt, e.PublishedAt, e.TraceParent, publishedAt => e.MarkPublished(publishedAt));
}
