using KartCategoryService.Domain.Common;

namespace KartCategoryService.Domain.Attributes;

/// <summary>
/// In-process signal that the ProductAttribute aggregate produced a change. Infrastructure
/// converts exactly one of these per create/update/deprecate into an attribute_outbox_events row
/// within the same SaveChanges transaction as CategoryUpdatedDomainEvent's Category counterpart
/// (same choke point: CategoryDbContext.SaveChangesAsync).
/// </summary>
public sealed record AttributeUpdatedDomainEvent(
    Guid AttributeId,
    string Name,
    Guid? CategoryId,
    AttributeDataType DataType,
    IReadOnlyList<AttributeValueSnapshot> Values,
    AttributeOperation Operation,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Immutable snapshot of an AttributeValue at event-raise time - the event must not hold a live reference into the aggregate's owned collection.</summary>
public sealed record AttributeValueSnapshot(Guid Id, string Value, int DisplayOrder);
