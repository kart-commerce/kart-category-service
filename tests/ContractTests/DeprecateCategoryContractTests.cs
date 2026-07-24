using System.Net;
using KartCategoryService.Domain.Categories;
using FluentAssertions;
using Xunit;

namespace KartCategoryService.ContractTests;

/// <summary>api-contract.yaml deprecateCategory: 204 for Admin, 404 for an unknown/already-deprecated categoryId.</summary>
public sealed class DeprecateCategoryContractTests : IClassFixture<CategoryContractTestFactory>
{
    private readonly CategoryContractTestFactory _factory;

    public DeprecateCategoryContractTests(CategoryContractTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DeleteCategory_AsAdmin_Returns204AndDeprecatesTheCategory()
    {
        var category = Category.CreateRoot("Deals", "system:test", DateTimeOffset.UtcNow).Value;
        _factory.Repository.Categories.Add(category);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await client.DeleteAsync($"/v1/categories/{category.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        category.Status.Should().Be(CategoryStatus.Deprecated);
    }

    [Fact]
    public async Task DeleteCategory_CalledTwice_SecondCallReturns404()
    {
        var category = Category.CreateRoot("Seasonal", "system:test", DateTimeOffset.UtcNow).Value;
        _factory.Repository.Categories.Add(category);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var first = await client.DeleteAsync($"/v1/categories/{category.Id}");
        var second = await client.DeleteAsync($"/v1/categories/{category.Id}");

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCategory_WithoutAdminRole_Returns403()
    {
        var category = Category.CreateRoot("Toys", "system:test", DateTimeOffset.UtcNow).Value;
        _factory.Repository.Categories.Add(category);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "customer");

        var response = await client.DeleteAsync($"/v1/categories/{category.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
