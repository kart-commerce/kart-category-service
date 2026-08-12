using KartCategoryService.Application.Common.Models;
using MediatR;

namespace KartCategoryService.Application.Features.ListAttributes;

/// <summary>GET /v1/attributes?categoryId=&amp;includeDeprecated= (api-contract.yaml listAttributes).</summary>
public sealed record ListAttributesQuery(Guid? CategoryId, bool IncludeDeprecated) : IRequest<IReadOnlyList<AttributeDto>>;
