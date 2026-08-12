using KartCategoryService.Domain.Attributes;

namespace KartCategoryService.Application.Common.Models;

/// <summary>
/// Shared read shape matching api-contract.yaml's `Attribute` schema - reused as the response of
/// every slice that returns an attribute (List/Create/Update), never redefined per-slice
/// (coding-standards.md: cross-slice reuse goes through a shared Application-level type), mirroring
/// CategoryDto's own convention.
/// </summary>
public sealed record AttributeDto(
    Guid AttributeId,
    string Name,
    Guid? CategoryId,
    string DataType,
    IReadOnlyList<AttributeValueDto> Values,
    string Status)
{
    public static AttributeDto FromDomain(ProductAttribute attribute) => new(
        attribute.Id,
        attribute.Name,
        attribute.CategoryId,
        attribute.DataType.ToString().ToLowerInvariant(),
        attribute.Values.OrderBy(v => v.DisplayOrder).Select(AttributeValueDto.FromDomain).ToList(),
        attribute.Status.ToString().ToLowerInvariant());
}

public sealed record AttributeValueDto(Guid ValueId, string Value, int DisplayOrder)
{
    public static AttributeValueDto FromDomain(AttributeValue value) => new(value.Id, value.Value, value.DisplayOrder);
}
