using KartCategoryService.Domain.Categories;

namespace KartCategoryService.Application.Common.Models;

/// <summary>
/// Shared read shape matching api-contract.yaml's `Category` schema exactly - reused as the
/// response of every slice that returns a category (List/Create/Rename/Move), never redefined
/// per-slice (coding-standards.md: cross-slice reuse goes through a shared Application-level type).
/// </summary>
public sealed record CategoryDto(
    Guid CategoryId,
    string Name,
    Guid? ParentId,
    IReadOnlyList<Guid> AncestorPath,
    int Depth,
    string Status)
{
    public static CategoryDto FromDomain(Category category) => new(
        category.Id,
        category.Name,
        category.ParentId,
        category.AncestorPath,
        category.Depth,
        category.Status.ToString().ToLowerInvariant());
}
