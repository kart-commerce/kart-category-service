using System.Text.Json;
using KartCategoryService.Domain.Categories;

namespace KartCategoryService.Infrastructure.Messaging;

/// <summary>
/// database-design.md `category_outbox_events` - one row per create/rename/move/deprecate,
/// written in the same transaction as the `categories` write it describes
/// (design-decisions.md, "Reliable Event Publication for CategoryUpdated"). Purely an
/// Infrastructure/messaging concern - the Domain aggregate only raises CategoryUpdatedDomainEvent
/// in-process; this row is how that gets converted into something the Outbox relay can publish.
/// </summary>
public sealed class CategoryOutboxEvent
{
    public const string CategoryUpdatedEventType = "CategoryUpdated";
    private const string RelaySystemPrincipal = "system:category-outbox-relay";

    /// <summary>event-contract.md's field names are camelCase (categoryId, occurredAt, ...).</summary>
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new(JsonSerializerDefaults.Web);

    public Guid OutboxId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string EventType { get; private set; } = CategoryUpdatedEventType;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = RelaySystemPrincipal;

    /// <summary>
    /// The originating HTTP request's `Activity.Current?.Id` (W3C traceparent), captured at this
    /// row's single write choke point (CategoryDbContext.SaveChangesAsync) - not at any individual
    /// command handler. Read back by the outbox relay (a background poller with no request context
    /// of its own) so the publish span continues the *original* request's trace instead of starting
    /// a disconnected new one. Null for rows written before this column existed.
    /// </summary>
    public string? TraceParent { get; private set; }

    private CategoryOutboxEvent()
    {
    }

    public static CategoryOutboxEvent FromDomainEvent(CategoryUpdatedDomainEvent domainEvent, string actingPrincipal, string? traceParent)
    {
        var payload = CategoryUpdatedEventPayload.FromDomainEvent(domainEvent);

        return new CategoryOutboxEvent
        {
            OutboxId = Guid.NewGuid(),
            CategoryId = domainEvent.CategoryId,
            EventType = CategoryUpdatedEventType,
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

    public static implicit operator OutboxEventRecord(CategoryOutboxEvent e) => new(
        e.OutboxId, e.EventType, e.Payload, e.OccurredAt, e.PublishedAt, e.TraceParent, publishedAt => e.MarkPublished(publishedAt));
}
