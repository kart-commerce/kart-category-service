using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using KartCategoryService.Domain.Categories;
using Xunit;

namespace KartCategoryService.ContractTests;

/// <summary>
/// api-contract.yaml reorderCategory: 200 on a valid reorder, 400 on a negative displayOrder, 404
/// for an unknown categoryId. Added for the "Category & Attribute Management (Admin)" flow -
/// kart-admin-service's ReorderCategoryCommand has always called this exact route/shape, but it
/// 404'd on every attempt until this endpoint existed.
/// </summary>
public sealed class ReorderCategoryContractTests : IClassFixture<CategoryContractTestFactory>
{
    private readonly CategoryContractTestFactory _factory;

    public ReorderCategoryContractTests(CategoryContractTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostReorder_AsAdmin_Returns200WithNewDisplayOrder()
    {
        var category = Category.CreateRoot("Electronics", "system:test", DateTimeOffset.UtcNow).Value;
        _factory.Repository.Categories.Add(category);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await client.PostAsJsonAsync($"/v1/categories/{category.Id}/reorder", new { displayOrder = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ReorderedCategory>();
        body!.DisplayOrder.Should().Be(3);
    }

    [Fact]
    public async Task PostReorder_WithNegativeDisplayOrder_Returns400()
    {
        var category = Category.CreateRoot("Electronics", "system:test", DateTimeOffset.UtcNow).Value;
        _factory.Repository.Categories.Add(category);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await client.PostAsJsonAsync($"/v1/categories/{category.Id}/reorder", new { displayOrder = -1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostReorder_WithUnknownCategoryId_Returns404()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await client.PostAsJsonAsync($"/v1/categories/{Guid.NewGuid()}/reorder", new { displayOrder = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record ReorderedCategory(Guid CategoryId, int DisplayOrder);
}
