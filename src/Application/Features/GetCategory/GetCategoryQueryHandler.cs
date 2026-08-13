using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using MediatR;

namespace KartCategoryService.Application.Features.GetCategory;

public sealed class GetCategoryQueryHandler(ICategoryRepository repository) : IRequestHandler<GetCategoryQuery, CategoryDto?>
{
    public async Task<CategoryDto?> Handle(GetCategoryQuery request, CancellationToken cancellationToken)
    {
        var category = await repository.GetActiveByIdAsync(request.CategoryId, cancellationToken);
        return category is null ? null : CategoryDto.FromDomain(category);
    }
}
