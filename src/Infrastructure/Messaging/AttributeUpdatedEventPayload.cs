using KartCategoryService.Domain.Attributes;

namespace KartCategoryService.Infrastructure.Messaging;

/// <summary>event-contract.md AttributeUpdated payload: attributeId, name, categoryId, dataType, values, operation, occurredAt - mirrors CategoryUpdatedEventPayload's shape.</summary>
public sealed record AttributeUpdatedEventPayload(
    Guid AttributeId,
    string Name,
    Guid? CategoryId,
    string DataType,
    IReadOnlyList<AttributeValuePayload> Values,
    string Operation,
    DateTimeOffset OccurredAt)
{
    public static AttributeUpdatedEventPayload FromDomainEvent(AttributeUpdatedDomainEvent domainEvent) => new(
        domainEvent.AttributeId,
        domainEvent.Name,
        domainEvent.CategoryId,
        domainEvent.DataType.ToString().ToLowerInvariant(),
        domainEvent.Values.Select(v => new AttributeValuePayload(v.Id, v.Value, v.DisplayOrder)).ToList(),
        domainEvent.Operation.ToString().ToLowerInvariant(),
        domainEvent.OccurredAt);
}

public sealed record AttributeValuePayload(Guid ValueId, string Value, int DisplayOrder);
