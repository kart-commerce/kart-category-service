using Kart.Shared.Observability;
using KartCategoryService.Api.Common;
using KartCategoryService.Api.Security;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Application.Features.CreateAttribute;
using KartCategoryService.Application.Features.DeprecateAttribute;
using KartCategoryService.Application.Features.ListAttributes;
using KartCategoryService.Application.Features.UpdateAttribute;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KartCategoryService.Api.Controllers;

/// <summary>
/// api-contract.yaml Attribute endpoints - new for the "Category &amp; Attribute Management
/// (Admin)" flow. Mirrors CategoriesController's shape exactly (write actions push the Flow tag,
/// GET/list is unauthenticated read - product-service/search-service/admin-web can all list
/// attributes for a category without an admin token, same posture as GET /v1/categories).
/// </summary>
[ApiController]
[Route("v1/attributes")]
public sealed class AttributesController : ControllerBase
{
    private const string FlowName = "CategoryAttributeManagementAdmin";

    private readonly ISender _sender;
    private readonly ILogger<AttributesController> _logger;

    public AttributesController(ISender sender, ILogger<AttributesController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>api-contract.yaml listAttributes - GET /v1/attributes?categoryId=&amp;includeDeprecated=.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AttributeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AttributeDto>>> ListAttributes(
        [FromQuery] Guid? categoryId,
        [FromQuery] bool includeDeprecated,
        CancellationToken cancellationToken)
    {
        var attributes = await _sender.Send(new ListAttributesQuery(categoryId, includeDeprecated), cancellationToken);
        return Ok(attributes);
    }

    /// <summary>api-contract.yaml createAttribute - POST /v1/attributes (RBAC-gated, Admin only).</summary>
    [HttpPost]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [ProducesResponseType(typeof(AttributeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttributeDto>> CreateAttribute(
        [FromBody] CreateAttributeRequest request,
        CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowName);
        _logger.LogInformation("Stage {Stage}: create-attribute request received (categoryId {CategoryId})", "AttributeCreateRequestReceived", request.CategoryId);

        var command = new CreateAttributeCommand(
            request.Name,
            request.CategoryId,
            request.DataType,
            request.Values ?? []);
        _logger.LogInformation("Stage {Stage}: dispatching CreateAttributeCommand (categoryId {CategoryId})", "CreateAttributeCommandDispatched", request.CategoryId);
        var result = await _sender.Send(command, cancellationToken);
        return this.ToActionResult<AttributeDto, AttributeDto>(
            result,
            attribute => CreatedAtAction(nameof(ListAttributes), new { categoryId = attribute.CategoryId }, attribute));
    }

    /// <summary>api-contract.yaml updateAttribute - PATCH /v1/attributes/{attributeId} (RBAC-gated, Admin only).</summary>
    [HttpPatch("{attributeId:guid}")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [ProducesResponseType(typeof(AttributeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttributeDto>> UpdateAttribute(
        [FromRoute] Guid attributeId,
        [FromBody] UpdateAttributeRequest request,
        CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowName);
        _logger.LogInformation("Stage {Stage}: update-attribute request received (attributeId {AttributeId})", "AttributeUpdateRequestReceived", attributeId);

        var command = new UpdateAttributeCommand(attributeId, request.Name, request.Values ?? []);
        _logger.LogInformation("Stage {Stage}: dispatching UpdateAttributeCommand (attributeId {AttributeId})", "UpdateAttributeCommandDispatched", attributeId);
        var result = await _sender.Send(command, cancellationToken);
        return this.ToActionResult<AttributeDto, AttributeDto>(result, attribute => Ok(attribute));
    }

    /// <summary>api-contract.yaml deprecateAttribute - DELETE /v1/attributes/{attributeId} (RBAC-gated, Admin only).</summary>
    [HttpDelete("{attributeId:guid}")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeprecateAttribute([FromRoute] Guid attributeId, CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowName);
        _logger.LogInformation("Stage {Stage}: deprecate-attribute request received (attributeId {AttributeId})", "AttributeDeprecateRequestReceived", attributeId);

        var command = new DeprecateAttributeCommand(attributeId);
        _logger.LogInformation("Stage {Stage}: dispatching DeprecateAttributeCommand (attributeId {AttributeId})", "DeprecateAttributeCommandDispatched", attributeId);
        var result = await _sender.Send(command, cancellationToken);
        return result.IsSuccess ? NoContent() : this.MapFailure(result.Error);
    }
}

/// <summary>api-contract.yaml createAttribute requestBody shape.</summary>
public sealed record CreateAttributeRequest(string Name, Guid? CategoryId, string DataType, IReadOnlyList<CreateAttributeValueRequest>? Values);

/// <summary>api-contract.yaml updateAttribute requestBody shape.</summary>
public sealed record UpdateAttributeRequest(string Name, IReadOnlyList<CreateAttributeValueRequest>? Values);
