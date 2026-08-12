using KartCategoryService.Domain.Common;
using MediatR;

namespace KartCategoryService.Application.Features.DeprecateAttribute;

/// <summary>DELETE /v1/attributes/{attributeId} (api-contract.yaml deprecateAttribute).</summary>
public sealed record DeprecateAttributeCommand(Guid AttributeId) : IRequest<Result>;
