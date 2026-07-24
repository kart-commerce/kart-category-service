using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using KartCategoryService.Domain.Categories;
using Xunit;

namespace KartCategoryService.ContractTests;

/// <summary>api-contract.yaml renameCategory: 200 for Admin, 404 for an unknown/deprecated categoryId.</summary>
public sealed class RenameCategoryContractTests : IClassFixture<CategoryContractTestFactory>
{
    private readonly CategoryContractTestFactory _factory;

    public RenameCategoryContractTests(CategoryContractTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PatchCategory_AsAdmin_Returns200WithRenamedCategory()
    {
        var category = Category.CreateRoot("Electronics", "system:test", DateTimeOffset.UtcNow).Value;
        _factory.Repository.Categories.Add(category);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await client.PatchAsJsonAsync($"/v1/categories/{category.Id}", new { name = "Consumer Electronics" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RenamedCategory>();
        body!.Name.Should().Be("Consumer Electronics");
    }

    [Fact]
    public async Task PatchCategory_WithUnknownCategoryId_Returns404()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await client.PatchAsJsonAsync($"/v1/categories/{Guid.NewGuid()}", new { name = "Doesn't matter" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record RenamedCategory(Guid CategoryId, string Name);
}
