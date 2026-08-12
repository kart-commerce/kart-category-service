using KartCategoryService.Application.Common.Models;
using KartCategoryService.Application.Features.CreateAttribute;
using KartCategoryService.Domain.Common;
using MediatR;

namespace KartCategoryService.Application.Features.UpdateAttribute;

/// <summary>PATCH /v1/attributes/{attributeId} (api-contract.yaml updateAttribute). Renames and replaces the entire Values list as one write - CategoryId/DataType are immutable after creation.</summary>
public sealed record UpdateAttributeCommand(
    Guid AttributeId,
    string Name,
    IReadOnlyList<CreateAttributeValueRequest> Values) : IRequest<Result<AttributeDto>>;
