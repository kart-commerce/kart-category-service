using KartCategoryService.Domain.Common;
using MediatR;

namespace KartCategoryService.Application.Features.DeprecateCategory;

/// <summary>
/// DELETE /categories/{categoryId} (api-contract.yaml deprecateCategory). Soft-delete only -
/// categoryId is never physically removed or reused (requirement-spec.md S4).
/// </summary>
public sealed record DeprecateCategoryCommand(Guid CategoryId) : IRequest<Result>;
