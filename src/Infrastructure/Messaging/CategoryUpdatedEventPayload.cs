using KartCategoryService.Domain.Categories;

namespace KartCategoryService.Infrastructure.Messaging;

/// <summary>
/// event-contract.md CategoryUpdated payload: categoryId, name, parentId, path, operation,
/// occurredAt. `operation` is serialized lowercase (created|renamed|moved|deprecated) to match
/// the platform's event-vocabulary convention used elsewhere in this contract.
/// </summary>
public sealed record CategoryUpdatedEventPayload(
    Guid CategoryId,
    string Name,
    Guid? ParentId,
    IReadOnlyList<Guid> Path,
    string Operation,
    DateTimeOffset OccurredAt)
{
    public static CategoryUpdatedEventPayload FromDomainEvent(CategoryUpdatedDomainEvent domainEvent) => new(
        domainEvent.CategoryId,
        domainEvent.Name,
        domainEvent.ParentId,
        domainEvent.Path,
        domainEvent.Operation.ToString().ToLowerInvariant(),
        domainEvent.OccurredAt);
}
