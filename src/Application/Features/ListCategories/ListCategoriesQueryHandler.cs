using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using MediatR;

namespace KartCategoryService.Application.Features.ListCategories;

public sealed class ListCategoriesQueryHandler : IRequestHandler<ListCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    private readonly ICategoryRepository _repository;
    private readonly ICategoryCache _cache;

    public ListCategoriesQueryHandler(ICategoryRepository repository, ICategoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<IReadOnlyList<CategoryDto>> Handle(ListCategoriesQuery request, CancellationToken cancellationToken)
    {
        // Admin/back-office tooling only (api-contract.yaml) - the write-through cache only ever
        // holds the public, active-only navigation view, so this path bypasses it entirely.
        if (request.IncludeDeprecated)
        {
            var allChildren = await _repository.GetChildrenAsync(request.ParentId, includeDeprecated: true, cancellationToken);
            return allChildren.Select(CategoryDto.FromDomain).ToList();
        }

        var cached = await _cache.GetChildrenAsync(request.ParentId, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var children = await _repository.GetChildrenAsync(request.ParentId, includeDeprecated: false, cancellationToken);
        var dtos = children.Select(CategoryDto.FromDomain).ToList();
        await _cache.SetChildrenAsync(request.ParentId, dtos, cancellationToken);
        return dtos;
    }
}
