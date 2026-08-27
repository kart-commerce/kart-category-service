using KartCategoryService.Application.Common.Models;
using MediatR;

namespace KartCategoryService.Application.Features.GetCategory;

/// <summary>GET /v1/categories/{categoryId} (api-contract.yaml getCategory). Single-category
/// lookup by id, e.g. for a storefront category page's own title - distinct from ListCategories'
/// parent-scoped children listing. Null result (not found or deprecated) maps to a 404 by the
/// same uniform rule ListCategories' single-item paths use.</summary>
public sealed record GetCategoryQuery(Guid CategoryId) : IRequest<CategoryDto?>;
