using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Categories;
using KartCategoryService.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartCategoryService.Application.Features.MoveCategory;

public sealed class MoveCategoryCommandHandler : IRequestHandler<MoveCategoryCommand, Result<CategoryDto>>
{
    private readonly ICategoryRepository _repository;
    private readonly ICategoryCache _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MoveCategoryCommandHandler> _logger;

    public MoveCategoryCommandHandler(
        ICategoryRepository repository,
        ICategoryCache cache,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider,
        ILogger<MoveCategoryCommandHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<CategoryDto>> Handle(MoveCategoryCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var target = await _repository.GetForUpdateAsync(request.CategoryId, cancellationToken);
        if (target is null)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogWarning(
                "Stage {Stage}: move-category rejected, category {CategoryId} not found or not active",
                "CategoryMoveRejected",
                request.CategoryId);
            return Result.Failure<CategoryDto>(Error.NotFound($"Category '{request.CategoryId}' not found or not active."));
        }

        // Checkpoint-logging taxonomy stage 5 (DecisionBranch) - moving to root vs. moving under
        // another parent is a meaningfully different code path (only the latter runs the
        // cycle/ancestor-lookup checks below).
        if (request.NewParentId is { } newParentId)
        {
            _logger.LogInformation(
                "Stage {Stage}: moving category {CategoryId} under new parent {NewParentId}",
                "CategoryMoveUnderParentBranch",
                request.CategoryId,
                newParentId);
        }
        else
        {
            _logger.LogInformation(
                "Stage {Stage}: moving category {CategoryId} to root level",
                "CategoryMoveToRootBranch",
                request.CategoryId);
        }

        Category? newParent = null;
        if (request.NewParentId is { } parentId)
        {
            newParent = await _repository.GetForUpdateAsync(parentId, cancellationToken);
            if (newParent is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                _logger.LogWarning(
                    "Stage {Stage}: move-category rejected, newParentId {NewParentId} not found or not active",
                    "CategoryMoveRejected",
                    parentId);
                return Result.Failure<CategoryDto>(Error.NotFound($"newParentId '{parentId}' not found or not active."));
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
            _logger.LogWarning(
                "Stage {Stage}: move-category rejected for category {CategoryId} ({ErrorCode}): {Reason}",
                "CategoryMoveRejected",
                request.CategoryId,
                moveResult.Error.Code,
                moveResult.Error.Message);
            return Result.Failure<CategoryDto>(moveResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        await RefreshAffectedCachesAsync(oldParentId, target, descendants, cancellationToken);

        _logger.LogInformation(
            "Stage {Stage}: category {CategoryId} moved to parent {NewParentId}",
            "CategoryMoveProcessCompleted",
            target.Id,
            target.ParentId);

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
            var siblingDtos = siblings.Select(CategoryDto.FromDomain).ToList();
            await _cache.SetChildrenAsync(parentId, siblingDtos, cancellationToken);
            _logger.LogInformation(
                "Stage {Stage}: category-children cache refreshed for parent {ParentId} ({Count} children)",
                "CategoryChildrenCachePersisted",
                parentId,
                siblingDtos.Count);
        }
    }
}
