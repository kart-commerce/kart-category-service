using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Common;
using MediatR;

namespace KartCategoryService.Application.Features.ReorderCategory;

/// <summary>
/// POST /categories/{categoryId}/reorder (api-contract.yaml reorderCategory). Purely a sibling
/// display-position change - never touches parentId/AncestorPath/Depth, see MoveCategory for
/// re-parenting. Added for the "Category & Attribute Management (Admin)" flow: kart-admin-service's
/// ReorderCategoryCommand has always called this exact route/shape, but this endpoint (and the
/// DisplayOrder field it needs) never existed here until now - every reorder attempt 404'd.
/// </summary>
public sealed record ReorderCategoryCommand(Guid CategoryId, int DisplayOrder) : IRequest<Result<CategoryDto>>;
