using KartCategoryService.Domain.Categories;

namespace KartCategoryService.Application.Common.Interfaces;

/// <summary>
/// Persistence abstraction for the Category aggregate (coding-standards.md DIP: Application owns
/// this interface, Infrastructure implements it). One repository per aggregate root - never a
/// generic IRepository&lt;T&gt;.
/// </summary>
public interface ICategoryRepository
{
    /// <summary>
    /// "List active children of X" (database-design.md idx_categories_parent_status) - the
    /// PostgreSQL read-replica fallback path for a /categories cache miss. parentId null lists
    /// depth-1 (top-level) categories. includeDeprecated=true is the admin/back-office path and
    /// bypasses the write-through cache entirely.
    /// </summary>
    Task<IReadOnlyList<Category>> GetChildrenAsync(Guid? parentId, bool includeDeprecated, CancellationToken cancellationToken);
}
