using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Common;
using MediatR;

namespace KartCategoryService.Application.Features.MoveCategory;

/// <summary>
/// POST /categories/{categoryId}/move (api-contract.yaml moveCategory). NewParentId null moves
/// this category (and its subtree) to depth-1 (top-level).
/// </summary>
public sealed record MoveCategoryCommand(Guid CategoryId, Guid? NewParentId) : IRequest<Result<CategoryDto>>;
