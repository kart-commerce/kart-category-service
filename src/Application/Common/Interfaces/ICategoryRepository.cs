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

    /// <summary>Null if the category does not exist or is deprecated - api-contract.yaml's uniform 404 for both.</summary>
    Task<Category?> GetActiveByIdAsync(Guid categoryId, CancellationToken cancellationToken);

    Task AddAsync(Category category, CancellationToken cancellationToken);

    /// <summary>
    /// Locks the row (SELECT ... FOR UPDATE) for the duration of the caller's transaction - must
    /// run inside a transaction opened via IUnitOfWork.BeginTransactionAsync (design-decisions.md,
    /// "Concurrency Control for Hierarchy Mutations"). Null if no row exists for this id at all;
    /// status is left to the caller/domain to check (MoveCategory needs to distinguish "not found"
    /// from "found but not active" itself).
    /// </summary>
    Task<Category?> GetForUpdateAsync(Guid categoryId, CancellationToken cancellationToken);

    /// <summary>
    /// Locks (SELECT ... FOR UPDATE) every active category whose ancestor_path contains categoryId -
    /// the moved subtree's descendants, whose AncestorPath/Depth a move updates as one operation
    /// (ddd-model.md). Must run inside the same transaction as GetForUpdateAsync above.
    /// </summary>
    Task<IReadOnlyList<Category>> GetDescendantsForUpdateAsync(Guid categoryId, CancellationToken cancellationToken);
}
