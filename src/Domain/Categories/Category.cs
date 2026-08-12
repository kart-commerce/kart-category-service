using KartCategoryService.Domain.Common;

namespace KartCategoryService.Domain.Categories;

/// <summary>
/// The service's one aggregate root (ddd-model.md) - a taxonomy node. Enforces, synchronously and
/// in domain code (ddd-cqrs-standards.md): no cycles, max depth 4, and that category ids are never
/// reassigned/reused (deprecation is the only removal path).
/// </summary>
public sealed class Category : AggregateRoot
{
    public const int MaxDepth = 4;

    private readonly List<Guid> _ancestorPath = new();

    public string Name { get; private set; } = string.Empty;
    public Guid? ParentId { get; private set; }
    public IReadOnlyList<Guid> AncestorPath => _ancestorPath.AsReadOnly();
    public int Depth { get; private set; }
    public int DisplayOrder { get; private set; }
    public CategoryStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = string.Empty;

    // EF Core materialization only - state is set via reflection over the fields above.
    private Category()
    {
    }

    private Category(
        Guid id,
        string name,
        Guid? parentId,
        IReadOnlyList<Guid> ancestorPath,
        string actingPrincipal,
        DateTimeOffset now)
    {
        Id = id;
        Name = name;
        ParentId = parentId;
        _ancestorPath = ancestorPath.ToList();
        Depth = _ancestorPath.Count + 1;
        DisplayOrder = 0;
        Status = CategoryStatus.Active;
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = actingPrincipal;
        UpdatedBy = actingPrincipal;
    }

    /// <summary>Creates a depth-1 (top-level department) category - api-contract.yaml POST /categories with no parentId.</summary>
    public static Result<Category> CreateRoot(string name, string actingPrincipal, DateTimeOffset now)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure<Category>(Error.Validation("Category name is required."));
        }

        var category = new Category(Guid.NewGuid(), trimmed, parentId: null, ancestorPath: Array.Empty<Guid>(), actingPrincipal, now);
        category.Raise(new CategoryUpdatedDomainEvent(category.Id, category.Name, category.ParentId, category.AncestorPath, category.DisplayOrder, CategoryOperation.Created, now));
        return Result.Success(category);
    }

    /// <summary>
    /// Creates a category under an existing, active parent. Rejects if the parent is deprecated or
    /// already at depth 4 (edge-cases.md, "Unbounded hierarchy depth degrades navigation read latency").
    /// </summary>
    public static Result<Category> CreateChild(string name, Category parent, string actingPrincipal, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var trimmed = name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure<Category>(Error.Validation("Category name is required."));
        }

        if (parent.Status != CategoryStatus.Active)
        {
            return Result.Failure<Category>(Error.NotFound("Parent category is not active."));
        }

        var newAncestorPath = parent.AncestorPath.Append(parent.Id).ToList();
        if (newAncestorPath.Count + 1 > MaxDepth)
        {
            return Result.Failure<Category>(Error.MaxDepthExceeded(
                $"Creating a category under '{parent.Id}' would exceed the maximum hierarchy depth of {MaxDepth}."));
        }

        var category = new Category(Guid.NewGuid(), trimmed, parent.Id, newAncestorPath, actingPrincipal, now);
        category.Raise(new CategoryUpdatedDomainEvent(category.Id, category.Name, category.ParentId, category.AncestorPath, category.DisplayOrder, CategoryOperation.Created, now));
        return Result.Success(category);
    }

    /// <summary>
    /// Renames this category. Does not touch parentId/AncestorPath - api-contract.yaml keeps rename
    /// and move as distinct operations since only move touches the cycle/depth invariants.
    /// </summary>
    public Result Rename(string newName, string actingPrincipal, DateTimeOffset now)
    {
        if (Status != CategoryStatus.Active)
        {
            return Result.Failure(Error.NotFound($"Category '{Id}' is not found or already deprecated."));
        }

        var trimmed = newName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure(Error.Validation("Category name is required."));
        }

        Name = trimmed;
        Touch(actingPrincipal, now);
        Raise(new CategoryUpdatedDomainEvent(Id, Name, ParentId, AncestorPath, DisplayOrder, CategoryOperation.Renamed, now));
        return Result.Success();
    }

    /// <summary>
    /// Changes this category's position among its siblings (api-contract.yaml reorderCategory).
    /// Purely cosmetic ordering - does not touch parentId/AncestorPath/Depth and carries none of
    /// MoveTo's cycle/depth invariants. Negative values are rejected; ties among siblings are
    /// broken by Name at the read side (ListCategoriesQueryHandler), so duplicate DisplayOrder
    /// values across siblings are tolerated, not a domain error.
    /// </summary>
    public Result Reorder(int newDisplayOrder, string actingPrincipal, DateTimeOffset now)
    {
        if (Status != CategoryStatus.Active)
        {
            return Result.Failure(Error.NotFound($"Category '{Id}' is not found or already deprecated."));
        }

        if (newDisplayOrder < 0)
        {
            return Result.Failure(Error.Validation("DisplayOrder must be zero or a positive integer."));
        }

        DisplayOrder = newDisplayOrder;
        Touch(actingPrincipal, now);
        Raise(new CategoryUpdatedDomainEvent(Id, Name, ParentId, AncestorPath, DisplayOrder, CategoryOperation.Reordered, now));
        return Result.Success();
    }

    /// <summary>
    /// Soft-deletes this category. The id is never physically removed or reused (edge-cases.md,
    /// "Deleting a category that still has products assigned") - it is the shard key Product/Search
    /// use downstream.
    /// </summary>
    public Result Deprecate(string actingPrincipal, DateTimeOffset now)
    {
        if (Status == CategoryStatus.Deprecated)
        {
            return Result.Failure(Error.NotFound($"Category '{Id}' is not found or already deprecated."));
        }

        Status = CategoryStatus.Deprecated;
        Touch(actingPrincipal, now);
        Raise(new CategoryUpdatedDomainEvent(Id, Name, ParentId, AncestorPath, DisplayOrder, CategoryOperation.Deprecated, now));
        return Result.Success();
    }

    /// <summary>
    /// Re-parents this category (and, transitively, its subtree) under newParent (null = top-level).
    /// Runs the synchronous ancestor-chain cycle check and max-depth-4 check (edge-cases.md, "Circular
    /// category reference on re-parent") and updates every descendant's AncestorPath/Depth as one
    /// logical operation, raising exactly one coarse CategoryUpdated on the moved subtree's root -
    /// never one event per descendant (edge-cases.md, "Subtree re-parenting invalidates downstream
    /// category data"). Caller is responsible for locking (SELECT ... FOR UPDATE) target, newParent,
    /// and descendants before invoking this - design-decisions.md, "Concurrency Control for Hierarchy
    /// Mutations" - locking itself is a persistence concern, not a domain one.
    /// </summary>
    public Result MoveTo(Category? newParent, IReadOnlyList<Category> descendants, string actingPrincipal, DateTimeOffset now)
    {
        if (Status != CategoryStatus.Active)
        {
            return Result.Failure(Error.NotFound($"Category '{Id}' is not found or already deprecated."));
        }

        if (newParent is not null)
        {
            if (newParent.Status != CategoryStatus.Active)
            {
                return Result.Failure(Error.NotFound($"New parent '{newParent.Id}' is not found or not active."));
            }

            if (newParent.Id == Id || newParent.AncestorPath.Contains(Id))
            {
                return Result.Failure(Error.CircularReference(
                    $"Moving '{Id}' under '{newParent.Id}' would create a cycle - '{newParent.Id}' is '{Id}' or one of its own descendants."));
            }
        }

        var newAncestorPath = newParent is null
            ? new List<Guid>()
            : newParent.AncestorPath.Append(newParent.Id).ToList();
        var newDepth = newAncestorPath.Count + 1;

        var depthOffset = newDepth - Depth;
        var deepestResultingDepth = descendants.Count == 0
            ? newDepth
            : Math.Max(newDepth, descendants.Max(d => d.Depth + depthOffset));

        if (deepestResultingDepth > MaxDepth)
        {
            return Result.Failure(Error.MaxDepthExceeded(
                $"Moving '{Id}' would push a node in its subtree past the maximum hierarchy depth of {MaxDepth}."));
        }

        var oldPrefix = AncestorPath.Append(Id).ToList();
        var newPrefixForThis = newAncestorPath.Append(Id).ToList();

        foreach (var descendant in descendants)
        {
            var suffix = descendant.AncestorPath.Skip(oldPrefix.Count).ToList();
            var descendantNewPath = newPrefixForThis.Concat(suffix).ToList();
            descendant.ApplyMovedAncestry(descendantNewPath, actingPrincipal, now);
        }

        ParentId = newParent?.Id;
        _ancestorPath.Clear();
        _ancestorPath.AddRange(newAncestorPath);
        Depth = newDepth;
        Touch(actingPrincipal, now);
        Raise(new CategoryUpdatedDomainEvent(Id, Name, ParentId, AncestorPath, DisplayOrder, CategoryOperation.Moved, now));
        return Result.Success();
    }

    /// <summary>
    /// Applies a recomputed ancestor path to a descendant caught up in an ancestor's move. Part of
    /// the "one logical operation" ddd-model.md describes - no separate CategoryUpdated is raised
    /// here; the moved subtree's root raises the single coarse event for the whole operation.
    /// </summary>
    internal void ApplyMovedAncestry(IReadOnlyList<Guid> newAncestorPath, string actingPrincipal, DateTimeOffset now)
    {
        ParentId = newAncestorPath.Count == 0 ? null : newAncestorPath[^1];
        _ancestorPath.Clear();
        _ancestorPath.AddRange(newAncestorPath);
        Depth = newAncestorPath.Count + 1;
        Touch(actingPrincipal, now);
    }

    private void Touch(string actingPrincipal, DateTimeOffset now)
    {
        UpdatedBy = actingPrincipal;
        UpdatedAt = now;
    }
}
