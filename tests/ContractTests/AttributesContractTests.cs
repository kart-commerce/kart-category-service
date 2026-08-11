using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using KartCategoryService.Domain.Attributes;
using KartCategoryService.Domain.Categories;
using Xunit;
using YamlDotNet.Serialization;

namespace KartCategoryService.ContractTests;

/// <summary>
/// api-contract.yaml's new Attribute endpoints (createAttribute/updateAttribute/deprecateAttribute/
/// listAttributes) - the second aggregate added for the "Category &amp; Attribute Management
/// (Admin)" flow, mirroring CategoriesContractTests/MoveCategoryContractTests' own shape.
/// </summary>
public sealed class AttributesContractTests : IClassFixture<CategoryContractTestFactory>
{
    private readonly CategoryContractTestFactory _factory;

    public AttributesContractTests(CategoryContractTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostAttribute_AsAdmin_WithSelectValues_Returns201()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await client.PostAsJsonAsync("/v1/attributes", new
        {
            name = "Color",
            categoryId = (Guid?)null,
            dataType = "select",
            values = new[] { new { value = "Red", displayOrder = 0 }, new { value = "Blue", displayOrder = 1 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("Color");
        body.GetProperty("values").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task PostAttribute_WithSelectAndNoValues_Returns400()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await client.PostAsJsonAsync("/v1/attributes", new
        {
            name = "Color",
            categoryId = (Guid?)null,
            dataType = "select",
            values = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostAttribute_WithUnknownCategoryId_Returns404()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await client.PostAsJsonAsync("/v1/attributes", new
        {
            name = "Warranty period",
            categoryId = Guid.NewGuid(),
            dataType = "text",
            values = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PatchAttribute_AsAdmin_ReplacesNameAndValues()
    {
        var attribute = ProductAttribute.Create("Color", null, AttributeDataType.Select, [("Red", 0)], "system:test", DateTimeOffset.UtcNow).Value;
        _factory.AttributeRepository.Attributes.Add(attribute);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await client.PatchAsJsonAsync($"/v1/attributes/{attribute.Id}", new
        {
            name = "Primary Color",
            values = new[] { new { value = "Red", displayOrder = 0 }, new { value = "Green", displayOrder = 1 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("Primary Color");
        body.GetProperty("values").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task DeleteAttribute_AsAdmin_Returns204()
    {
        var attribute = ProductAttribute.Create("Warranty period", null, AttributeDataType.Text, [], "system:test", DateTimeOffset.UtcNow).Value;
        _factory.AttributeRepository.Attributes.Add(attribute);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await client.DeleteAsync($"/v1/attributes/{attribute.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetAttributes_ResponseShape_MatchesAttributeSchemaInApiContract()
    {
        var expectedFields = LoadAttributeSchemaPropertyNames();
        var attribute = ProductAttribute.Create("Color", null, AttributeDataType.Select, [("Red", 0)], "system:test", DateTimeOffset.UtcNow).Value;
        _factory.AttributeRepository.Attributes.Add(attribute);

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v1/attributes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetArrayLength().Should().BeGreaterThan(0);

        var firstItemFields = payload[0].EnumerateObject().Select(p => p.Name).ToHashSet();
        firstItemFields.Should().BeEquivalentTo(expectedFields);
    }

    private static HashSet<string> LoadAttributeSchemaPropertyNames()
    {
        var yamlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "api-contract.yaml");
        var yaml = File.ReadAllText(yamlPath);

        IDeserializer deserializer = new DeserializerBuilder().Build();
        var root = deserializer.Deserialize<Dictionary<object, object>>(yaml);

        var components = (Dictionary<object, object>)root["components"];
        var schemas = (Dictionary<object, object>)components["schemas"];
        var attribute = (Dictionary<object, object>)schemas["Attribute"];
        var properties = (Dictionary<object, object>)attribute["properties"];

        return properties.Keys.Select(k => (string)k).ToHashSet();
    }
}
