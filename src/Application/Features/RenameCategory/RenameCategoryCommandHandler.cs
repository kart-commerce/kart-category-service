using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartCategoryService.Application.Features.RenameCategory;

public sealed class RenameCategoryCommandHandler : IRequestHandler<RenameCategoryCommand, Result<CategoryDto>>
{
    private readonly ICategoryRepository _repository;
    private readonly ICategoryCache _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RenameCategoryCommandHandler> _logger;

    public RenameCategoryCommandHandler(
        ICategoryRepository repository,
        ICategoryCache cache,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider,
        ILogger<RenameCategoryCommandHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<CategoryDto>> Handle(RenameCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _repository.GetActiveByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            _logger.LogWarning(
                "Stage {Stage}: rename-category rejected, category {CategoryId} not found or already deprecated",
                "CategoryRenameRejected",
                request.CategoryId);
            return Result.Failure<CategoryDto>(Error.NotFound($"Category '{request.CategoryId}' is not found or already deprecated."));
        }

        var renameResult = category.Rename(request.Name, _currentPrincipal.ActingPrincipal, _timeProvider.GetUtcNow());
        if (renameResult.IsFailure)
        {
            _logger.LogWarning(
                "Stage {Stage}: rename-category rejected for category {CategoryId} ({ErrorCode}): {Reason}",
                "CategoryRenameRejected",
                request.CategoryId,
                renameResult.Error.Code,
                renameResult.Error.Message);
            return Result.Failure<CategoryDto>(renameResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var siblings = await _repository.GetChildrenAsync(category.ParentId, includeDeprecated: false, cancellationToken);
        var siblingDtos = siblings.Select(CategoryDto.FromDomain).ToList();
        await _cache.SetChildrenAsync(category.ParentId, siblingDtos, cancellationToken);
        _logger.LogInformation(
            "Stage {Stage}: category-children cache refreshed for parent {ParentId} ({Count} children)",
            "CategoryChildrenCachePersisted",
            category.ParentId,
            siblingDtos.Count);

        _logger.LogInformation(
            "Stage {Stage}: category {CategoryId} renamed to {Name}",
            "CategoryRenameProcessCompleted",
            category.Id,
            category.Name);

        return Result.Success(CategoryDto.FromDomain(category));
    }
}
