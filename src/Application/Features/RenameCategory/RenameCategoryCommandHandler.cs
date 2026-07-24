using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Common;
using MediatR;

namespace KartCategoryService.Application.Features.RenameCategory;

public sealed class RenameCategoryCommandHandler : IRequestHandler<RenameCategoryCommand, Result<CategoryDto>>
{
    private readonly ICategoryRepository _repository;
    private readonly ICategoryCache _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;

    public RenameCategoryCommandHandler(
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

    public async Task<Result<CategoryDto>> Handle(RenameCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _repository.GetActiveByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure<CategoryDto>(Error.NotFound($"Category '{request.CategoryId}' is not found or already deprecated."));
        }

        var renameResult = category.Rename(request.Name, _currentPrincipal.ActingPrincipal, _timeProvider.GetUtcNow());
        if (renameResult.IsFailure)
        {
            return Result.Failure<CategoryDto>(renameResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var siblings = await _repository.GetChildrenAsync(category.ParentId, includeDeprecated: false, cancellationToken);
        await _cache.SetChildrenAsync(category.ParentId, siblings.Select(CategoryDto.FromDomain).ToList(), cancellationToken);

        return Result.Success(CategoryDto.FromDomain(category));
    }
}
