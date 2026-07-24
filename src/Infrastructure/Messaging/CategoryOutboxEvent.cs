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

    private CategoryOutboxEvent()
    {
    }

    public static CategoryOutboxEvent FromDomainEvent(CategoryUpdatedDomainEvent domainEvent, string actingPrincipal)
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
        };
    }

    public void MarkPublished(DateTimeOffset publishedAt)
    {
        PublishedAt = publishedAt;
    }
}
