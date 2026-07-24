using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Common;
using MediatR;

namespace KartCategoryService.Application.Features.DeprecateCategory;

public sealed class DeprecateCategoryCommandHandler : IRequestHandler<DeprecateCategoryCommand, Result>
{
    private readonly ICategoryRepository _repository;
    private readonly ICategoryCache _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;

    public DeprecateCategoryCommandHandler(
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

    public async Task<Result> Handle(DeprecateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _repository.GetActiveByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure(Error.NotFound($"Category '{request.CategoryId}' is not found or already deprecated."));
        }

        var deprecateResult = category.Deprecate(_currentPrincipal.ActingPrincipal, _timeProvider.GetUtcNow());
        if (deprecateResult.IsFailure)
        {
            return deprecateResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var siblings = await _repository.GetChildrenAsync(category.ParentId, includeDeprecated: false, cancellationToken);
        await _cache.SetChildrenAsync(category.ParentId, siblings.Select(CategoryDto.FromDomain).ToList(), cancellationToken);

        return Result.Success();
    }
}
