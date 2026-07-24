using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using KartCategoryService.Domain.Categories;
using Xunit;

namespace KartCategoryService.ContractTests;

/// <summary>
/// api-contract.yaml moveCategory: 200 on a valid move, 400 when it would create a cycle, 404 for
/// an unknown categoryId/newParentId.
/// </summary>
public sealed class MoveCategoryContractTests : IClassFixture<CategoryContractTestFactory>
{
    private readonly CategoryContractTestFactory _factory;

    public MoveCategoryContractTests(CategoryContractTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostMove_AsAdmin_Returns200WithNewParent()
    {
        var now = DateTimeOffset.UtcNow;
        var oldParent = Category.CreateRoot("OldParent", "system:test", now).Value;
        var newParent = Category.CreateRoot("NewParent", "system:test", now).Value;
        var category = Category.CreateChild("Category", oldParent, "system:test", now).Value;
        _factory.Repository.Categories.AddRange(new[] { oldParent, newParent, category });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await client.PostAsJsonAsync($"/v1/categories/{category.Id}/move", new { newParentId = newParent.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MovedCategory>();
        body!.ParentId.Should().Be(newParent.Id);
    }

    [Fact]
    public async Task PostMove_OntoOwnDescendant_Returns400()
    {
        var now = DateTimeOffset.UtcNow;
        var department = Category.CreateRoot("Department2", "system:test", now).Value;
        var category = Category.CreateChild("Category2", department, "system:test", now).Value;
        var subcategory = Category.CreateChild("Subcategory2", category, "system:test", now).Value;
        _factory.Repository.Categories.AddRange(new[] { department, category, subcategory });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await client.PostAsJsonAsync($"/v1/categories/{category.Id}/move", new { newParentId = subcategory.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostMove_WithUnknownCategoryId_Returns404()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await client.PostAsJsonAsync($"/v1/categories/{Guid.NewGuid()}/move", new { newParentId = (Guid?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record MovedCategory(Guid CategoryId, Guid? ParentId);
}
