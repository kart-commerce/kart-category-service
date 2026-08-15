using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartCategoryService.Application.Features.ReorderCategory;

public sealed class ReorderCategoryCommandHandler : IRequestHandler<ReorderCategoryCommand, Result<CategoryDto>>
{
    private readonly ICategoryRepository _repository;
    private readonly ICategoryCache _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReorderCategoryCommandHandler> _logger;

    public ReorderCategoryCommandHandler(
        ICategoryRepository repository,
        ICategoryCache cache,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider,
        ILogger<ReorderCategoryCommandHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<CategoryDto>> Handle(ReorderCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _repository.GetActiveByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            _logger.LogWarning(
                "Stage {Stage}: reorder-category rejected, category {CategoryId} not found or already deprecated",
                "CategoryReorderRejected",
                request.CategoryId);
            return Result.Failure<CategoryDto>(Error.NotFound($"Category '{request.CategoryId}' is not found or already deprecated."));
        }

        var reorderResult = category.Reorder(request.DisplayOrder, _currentPrincipal.ActingPrincipal, _timeProvider.GetUtcNow());
        if (reorderResult.IsFailure)
        {
            _logger.LogWarning(
                "Stage {Stage}: reorder-category rejected for category {CategoryId} ({ErrorCode}): {Reason}",
                "CategoryReorderRejected",
                request.CategoryId,
                reorderResult.Error.Code,
                reorderResult.Error.Message);
            return Result.Failure<CategoryDto>(reorderResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var siblings = await _repository.GetChildrenAsync(category.ParentId, includeDeprecated: false, cancellationToken);
        var siblingDtos = siblings.Select(CategoryDto.FromDomain).ToList();
        await _cache.SetChildrenAsync(category.ParentId, siblingDtos, cancellationToken);

        _logger.LogInformation(
            "Stage {Stage}: category {CategoryId} reordered to displayOrder {DisplayOrder}, category-children cache refreshed for parent {ParentId} ({Count} children)",
            "CategoryReorderProcessCompleted",
            category.Id,
            category.DisplayOrder,
            category.ParentId,
            siblingDtos.Count);

        return Result.Success(CategoryDto.FromDomain(category));
    }
}
