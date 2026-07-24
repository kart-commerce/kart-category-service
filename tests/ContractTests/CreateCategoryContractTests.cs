using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace KartCategoryService.ContractTests;

/// <summary>
/// api-contract.yaml createCategory: 201 for an Admin-scoped caller, 403 otherwise - the RBAC gate
/// requirement-spec.md S4/ddd-model.md fix for every write endpoint.
/// </summary>
public sealed class CreateCategoryContractTests : IClassFixture<CategoryContractTestFactory>
{
    private readonly CategoryContractTestFactory _factory;

    public CreateCategoryContractTests(CategoryContractTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostCategories_AsAdmin_Returns201WithCreatedCategory()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await client.PostAsJsonAsync("/v1/categories", new { name = "Electronics" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreatedCategory>();
        body!.Name.Should().Be("Electronics");
        body.Status.Should().Be("active");
        body.Depth.Should().Be(1);
    }

    [Fact]
    public async Task PostCategories_WithoutAdminRole_Returns403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "customer");

        var response = await client.PostAsJsonAsync("/v1/categories", new { name = "Electronics" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostCategories_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/categories", new { name = "Electronics" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record CreatedCategory(Guid CategoryId, string Name, Guid? ParentId, int Depth, string Status);
}
