using KartCategoryService.Application.Common.Models;
using KartCategoryService.Application.Features.ListCategories;
using MediatR;
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

    /// <summary>api-contract.yaml listCategories - GET /v1/categories.</summary>
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
}
