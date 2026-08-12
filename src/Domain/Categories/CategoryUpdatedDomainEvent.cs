using KartCategoryService.Domain.Common;

namespace KartCategoryService.Domain.Categories;

/// <summary>
/// In-process signal that the Category aggregate produced a taxonomy change. Infrastructure
/// converts exactly one of these per create/rename/deprecate/move into a category_outbox_events
/// row within the same SaveChanges transaction (event-contract.md CategoryUpdated: categoryId,
/// name, parentId, path, operation, occurredAt).
/// </summary>
public sealed record CategoryUpdatedDomainEvent(
    Guid CategoryId,
    string Name,
    Guid? ParentId,
    IReadOnlyList<Guid> Path,
    int DisplayOrder,
    CategoryOperation Operation,
    DateTimeOffset OccurredAt) : IDomainEvent;
