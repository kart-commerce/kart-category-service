namespace KartCategoryService.Domain.Categories;

/// <summary>
/// Which taxonomy write triggered a CategoryUpdated event (event-contract.md, "operation is added
/// so a consumer does not have to infer which taxonomy write triggered the event from field diffs").
/// </summary>
public enum CategoryOperation
{
    Created,
    Renamed,
    Moved,
    Deprecated,
    Reordered
}
