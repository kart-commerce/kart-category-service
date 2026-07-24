using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Common;
using MediatR;

namespace KartCategoryService.Application.Features.RenameCategory;

/// <summary>
/// PATCH /categories/{categoryId} (api-contract.yaml renameCategory). Does not touch
/// parentId/AncestorPath - see MoveCategory for re-parenting.
/// </summary>
public sealed record RenameCategoryCommand(Guid CategoryId, string Name) : IRequest<Result<CategoryDto>>;
