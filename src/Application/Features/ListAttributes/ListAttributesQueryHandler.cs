using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using MediatR;

namespace KartCategoryService.Application.Features.ListAttributes;

/// <summary>No cache layer here (unlike ListCategoriesQueryHandler) - attribute management is admin/back-office tooling only, not a high-traffic public read path, so a straight repository read is proportionate.</summary>
public sealed class ListAttributesQueryHandler : IRequestHandler<ListAttributesQuery, IReadOnlyList<AttributeDto>>
{
    private readonly IAttributeRepository _repository;

    public ListAttributesQueryHandler(IAttributeRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<AttributeDto>> Handle(ListAttributesQuery request, CancellationToken cancellationToken)
    {
        var attributes = await _repository.ListAsync(request.CategoryId, request.IncludeDeprecated, cancellationToken);
        return attributes.Select(AttributeDto.FromDomain).ToList();
    }
}
