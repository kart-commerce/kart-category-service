using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartCategoryService.Application.Features.DeprecateCategory;

public sealed class DeprecateCategoryCommandHandler : IRequestHandler<DeprecateCategoryCommand, Result>
{
    private readonly ICategoryRepository _repository;
    private readonly ICategoryCache _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DeprecateCategoryCommandHandler> _logger;

    public DeprecateCategoryCommandHandler(
        ICategoryRepository repository,
        ICategoryCache cache,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider,
        ILogger<DeprecateCategoryCommandHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result> Handle(DeprecateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _repository.GetActiveByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            _logger.LogWarning(
                "Stage {Stage}: deprecate-category rejected, category {CategoryId} not found or already deprecated",
                "CategoryDeprecateRejected",
                request.CategoryId);
            return Result.Failure(Error.NotFound($"Category '{request.CategoryId}' is not found or already deprecated."));
        }

        var deprecateResult = category.Deprecate(_currentPrincipal.ActingPrincipal, _timeProvider.GetUtcNow());
        if (deprecateResult.IsFailure)
        {
            _logger.LogWarning(
                "Stage {Stage}: deprecate-category rejected for category {CategoryId} ({ErrorCode}): {Reason}",
                "CategoryDeprecateRejected",
                request.CategoryId,
                deprecateResult.Error.Code,
                deprecateResult.Error.Message);
            return deprecateResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var siblings = await _repository.GetChildrenAsync(category.ParentId, includeDeprecated: false, cancellationToken);
        var siblingDtos = siblings.Select(CategoryDto.FromDomain).ToList();
        await _cache.SetChildrenAsync(category.ParentId, siblingDtos, cancellationToken);

        _logger.LogInformation(
            "Stage {Stage}: category {CategoryId} deprecated, category-children cache refreshed for parent {ParentId} ({Count} children)",
            "CategoryDeprecateProcessCompleted",
            category.Id,
            category.ParentId,
            siblingDtos.Count);

        return Result.Success();
    }
}
