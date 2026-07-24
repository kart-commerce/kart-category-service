using KartCategoryService.Application.Common.Models;

namespace KartCategoryService.Application.Common.Interfaces;

/// <summary>
/// Write-through Redis cache in front of PostgreSQL for the /categories read path
/// (design-decisions.md, "Caching Strategy for the /categories Read Path"). A miss here always
/// falls back to ICategoryRepository - Redis availability is a latency, not a correctness,
/// dependency (ADR-0011). Every taxonomy write updates the affected entries synchronously in the
/// same operation as the PostgreSQL write, never a bare TTL-expiry.
/// </summary>
public interface ICategoryCache
{
    /// <summary>category:children:{parentId} (sentinel "root" for depth-1 nodes) - database-design.md.</summary>
    Task<IReadOnlyList<CategoryDto>?> GetChildrenAsync(Guid? parentId, CancellationToken cancellationToken);

    Task SetChildrenAsync(Guid? parentId, IReadOnlyList<CategoryDto> children, CancellationToken cancellationToken);
}
