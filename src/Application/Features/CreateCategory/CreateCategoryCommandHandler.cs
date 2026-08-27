using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Categories;
using KartCategoryService.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartCategoryService.Application.Features.CreateCategory;

public sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Result<CategoryDto>>
{
    private readonly ICategoryRepository _repository;
    private readonly ICategoryCache _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreateCategoryCommandHandler> _logger;

    public CreateCategoryCommandHandler(
        ICategoryRepository repository,
        ICategoryCache cache,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider,
        ILogger<CreateCategoryCommandHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<CategoryDto>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var actingPrincipal = _currentPrincipal.ActingPrincipal;

        Result<Category> creationResult;
        if (request.ParentId is { } parentId)
        {
            _logger.LogInformation(
                "Stage {Stage}: creating category under parent {ParentId}",
                "CategoryCreateUnderParentBranch",
                parentId);

            var parent = await _repository.GetActiveByIdAsync(parentId, cancellationToken);
            if (parent is null)
            {
                _logger.LogWarning(
                    "Stage {Stage}: create-category rejected, parentId {ParentId} not found or not active",
                    "CategoryCreateRejected",
                    parentId);
                return Result.Failure<CategoryDto>(Error.NotFound($"parentId '{parentId}' not found or not active."));
            }

            creationResult = Category.CreateChild(request.Name, parent, actingPrincipal, now);
        }
        else
        {
            _logger.LogInformation("Stage {Stage}: creating root-level category", "CategoryCreateRootBranch");
            creationResult = Category.CreateRoot(request.Name, actingPrincipal, now);
        }

        if (creationResult.IsFailure)
        {
            _logger.LogWarning(
                "Stage {Stage}: create-category rejected ({ErrorCode}): {Reason}",
                "CategoryCreateRejected",
                creationResult.Error.Code,
                creationResult.Error.Message);
            return Result.Failure<CategoryDto>(creationResult.Error);
        }

        var category = creationResult.Value;
        await _repository.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var siblingCount = await RefreshParentChildrenCacheAsync(category.ParentId, cancellationToken);

        _logger.LogInformation(
            "Stage {Stage}: category {CategoryId} ({Name}) created under parent {ParentId}, category-children cache refreshed ({Count} children)",
            "CategoryCreateProcessCompleted",
            category.Id,
            category.Name,
            category.ParentId,
            siblingCount);

        return Result.Success(CategoryDto.FromDomain(category));
    }

    private async Task<int> RefreshParentChildrenCacheAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        var siblings = await _repository.GetChildrenAsync(parentId, includeDeprecated: false, cancellationToken);
        var dtos = siblings.Select(CategoryDto.FromDomain).ToList();
        await _cache.SetChildrenAsync(parentId, dtos, cancellationToken);
        return dtos.Count;
    }
}
