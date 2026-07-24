namespace KartCategoryService.Domain.Categories;

/// <summary>
/// Deprecation is the only removal path - a Category's id is never physically deleted or reused
/// (requirement-spec.md S4; it is the shard key Product/Search key off downstream).
/// </summary>
public enum CategoryStatus
{
    Active,
    Deprecated
}
