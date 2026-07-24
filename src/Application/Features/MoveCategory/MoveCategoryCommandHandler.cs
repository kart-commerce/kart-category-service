using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Categories;
using KartCategoryService.Domain.Common;
using MediatR;

namespace KartCategoryService.Application.Features.MoveCategory;

public sealed class MoveCategoryCommandHandler : IRequestHandler<MoveCategoryCommand, Result<CategoryDto>>
{
    private readonly ICategoryRepository _repository;
    private readonly ICategoryCache _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;

    public MoveCategoryCommandHandler(
        ICategoryRepository repository,
        ICategoryCache cache,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
    }

    public async Task<Result<CategoryDto>> Handle(MoveCategoryCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var target = await _repository.GetForUpdateAsync(request.CategoryId, cancellationToken);
        if (target is null)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<CategoryDto>(Error.NotFound($"Category '{request.CategoryId}' not found or not active."));
        }

        Category? newParent = null;
        if (request.NewParentId is { } newParentId)
        {
            newParent = await _repository.GetForUpdateAsync(newParentId, cancellationToken);
            if (newParent is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<CategoryDto>(Error.NotFound($"newParentId '{newParentId}' not found or not active."));
            }
        }

        var descendants = await _repository.GetDescendantsForUpdateAsync(request.CategoryId, cancellationToken);
        var oldParentId = target.ParentId;
        var actingPrincipal = _currentPrincipal.ActingPrincipal;
        var now = _timeProvider.GetUtcNow();

        var moveResult = target.MoveTo(newParent, descendants, actingPrincipal, now);
        if (moveResult.IsFailure)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<CategoryDto>(moveResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        await RefreshAffectedCachesAsync(oldParentId, target, descendants, cancellationToken);

        return Result.Success(CategoryDto.FromDomain(target));
    }

    /// <summary>
    /// A move can change the old/new parent's children list membership *and* the ancestorPath field
    /// of every entry in each affected descendant's own immediate-parent children list (database-design.md's
    /// write-through cache holds the full Category shape per entry, not just id/name/hasChildren).
    /// </summary>
    private async Task RefreshAffectedCachesAsync(
        Guid? oldParentId,
        Category target,
        IReadOnlyList<Category> descendants,
        CancellationToken cancellationToken)
    {
        var parentIdsToRefresh = new HashSet<Guid?> { oldParentId, target.ParentId };
        foreach (var descendant in descendants)
        {
            parentIdsToRefresh.Add(descendant.ParentId);
        }

        foreach (var parentId in parentIdsToRefresh)
        {
            var siblings = await _repository.GetChildrenAsync(parentId, includeDeprecated: false, cancellationToken);
            await _cache.SetChildrenAsync(parentId, siblings.Select(CategoryDto.FromDomain).ToList(), cancellationToken);
        }
    }
}
