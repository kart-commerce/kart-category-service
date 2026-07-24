using KartCategoryService.Api.Common;
using KartCategoryService.Api.Security;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Application.Features.CreateCategory;
using KartCategoryService.Application.Features.ListCategories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KartCategoryService.Api.Controllers;

[ApiController]
[Route("v1/categories")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ISender _sender;

    public CategoriesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>api-contract.yaml listCategories - GET /v1/categories. CanRead is unconditional (ddd-model.md).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> ListCategories(
        [FromQuery] Guid? parentId,
        [FromQuery] bool includeDeprecated,
        CancellationToken cancellationToken)
    {
        var categories = await _sender.Send(new ListCategoriesQuery(parentId, includeDeprecated), cancellationToken);
        return Ok(categories);
    }

    /// <summary>api-contract.yaml createCategory - POST /v1/categories (RBAC-gated, Admin only).</summary>
    [HttpPost]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> CreateCategory(
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateCategoryCommand(request.Name, request.ParentId), cancellationToken);
        return this.ToActionResult<CategoryDto, CategoryDto>(
            result,
            category => CreatedAtAction(nameof(ListCategories), new { parentId = category.ParentId }, category));
    }
}

/// <summary>api-contract.yaml createCategory requestBody shape.</summary>
public sealed record CreateCategoryRequest(string Name, Guid? ParentId);
