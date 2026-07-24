using KartCategoryService.Application.Common.Models;
using MediatR;

namespace KartCategoryService.Application.Features.ListCategories;

/// <summary>
/// GET /categories (api-contract.yaml listCategories). ParentId omitted lists depth-1 (top-level)
/// categories. IncludeDeprecated is the admin/back-office path - the public storefront view never
/// sets it.
/// </summary>
public sealed record ListCategoriesQuery(Guid? ParentId, bool IncludeDeprecated) : IRequest<IReadOnlyList<CategoryDto>>;
