namespace KartCategoryService.Domain.Attributes;

/// <summary>
/// How a ProductAttribute's values should be interpreted/rendered. `Select` is the only data type
/// that uses the Values collection - Text/Number/Boolean attributes describe a product-level field
/// with no fixed value set (e.g. "Warranty period"), Select describes a fixed pick-list (e.g.
/// "Color" -> Red/Blue/Green), enforced in domain code by ProductAttribute.Create/UpdateValues.
/// </summary>
public enum AttributeDataType
{
    Text,
    Number,
    Boolean,
    Select
}
