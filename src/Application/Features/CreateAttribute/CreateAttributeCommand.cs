using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Common;
using MediatR;

namespace KartCategoryService.Application.Features.CreateAttribute;

/// <summary>POST /v1/attributes (api-contract.yaml createAttribute). CategoryId null creates a global attribute available under any category.</summary>
public sealed record CreateAttributeCommand(
    string Name,
    Guid? CategoryId,
    string DataType,
    IReadOnlyList<CreateAttributeValueRequest> Values) : IRequest<Result<AttributeDto>>;

public sealed record CreateAttributeValueRequest(string Value, int DisplayOrder);
