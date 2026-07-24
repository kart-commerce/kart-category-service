using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Common;
using MediatR;

namespace KartCategoryService.Application.Features.CreateCategory;

/// <summary>
/// POST /categories (api-contract.yaml createCategory). ParentId omitted/null creates a depth-1
/// (top-level department) category.
/// </summary>
public sealed record CreateCategoryCommand(string Name, Guid? ParentId) : IRequest<Result<CategoryDto>>;
