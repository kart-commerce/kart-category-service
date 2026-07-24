using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Categories;
using KartCategoryService.Domain.Common;
using MediatR;

namespace KartCategoryService.Application.Features.CreateCategory;

public sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Result<CategoryDto>>
{
    private readonly ICategoryRepository _repository;
    private readonly ICategoryCache _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;

    public CreateCategoryCommandHandler(
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

    public async Task<Result<CategoryDto>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var actingPrincipal = _currentPrincipal.ActingPrincipal;

        Result<Category> creationResult;
        if (request.ParentId is { } parentId)
        {
            var parent = await _repository.GetActiveByIdAsync(parentId, cancellationToken);
            if (parent is null)
            {
                return Result.Failure<CategoryDto>(Error.NotFound($"parentId '{parentId}' not found or not active."));
            }

            creationResult = Category.CreateChild(request.Name, parent, actingPrincipal, now);
        }
        else
        {
            creationResult = Category.CreateRoot(request.Name, actingPrincipal, now);
        }

        if (creationResult.IsFailure)
        {
            return Result.Failure<CategoryDto>(creationResult.Error);
        }

        var category = creationResult.Value;
        await _repository.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RefreshParentChildrenCacheAsync(category.ParentId, cancellationToken);

        return Result.Success(CategoryDto.FromDomain(category));
    }

    private async Task RefreshParentChildrenCacheAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        var siblings = await _repository.GetChildrenAsync(parentId, includeDeprecated: false, cancellationToken);
        var dtos = siblings.Select(CategoryDto.FromDomain).ToList();
        await _cache.SetChildrenAsync(parentId, dtos, cancellationToken);
    }
}
