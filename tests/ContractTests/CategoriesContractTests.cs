using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using KartCategoryService.Domain.Categories;
using Xunit;
using YamlDotNet.Serialization;

namespace KartCategoryService.ContractTests;

/// <summary>
/// Validates GET /v1/categories's actual HTTP response against the field list api-contract.yaml's
/// own Category schema defines - the vendored copy under Fixtures/ is the same file this service's
/// design docs were approved with (docs/services/kart-category-service/api-contract.yaml). This
/// verifies the wire shape stays in sync with the contract, not just that it looked right at
/// implementation time.
/// </summary>
public sealed class CategoriesContractTests : IClassFixture<CategoryContractTestFactory>
{
    private readonly CategoryContractTestFactory _factory;

    public CategoriesContractTests(CategoryContractTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCategories_ResponseShape_MatchesCategorySchemaInApiContract()
    {
        var expectedFields = LoadCategorySchemaPropertyNames();

        _factory.Repository.Categories.Add(Category.CreateRoot("Electronics", "system:test", DateTimeOffset.UtcNow).Value);
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/v1/categories");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.ValueKind.Should().Be(JsonValueKind.Array);
        payload.GetArrayLength().Should().BeGreaterThan(0);

        var firstItemFields = payload[0].EnumerateObject().Select(p => p.Name).ToHashSet();
        firstItemFields.Should().BeEquivalentTo(expectedFields);
    }

    private static HashSet<string> LoadCategorySchemaPropertyNames()
    {
        var yamlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "api-contract.yaml");
        var yaml = File.ReadAllText(yamlPath);

        IDeserializer deserializer = new DeserializerBuilder().Build();
        var root = deserializer.Deserialize<Dictionary<object, object>>(yaml);

        var components = (Dictionary<object, object>)root["components"];
        var schemas = (Dictionary<object, object>)components["schemas"];
        var category = (Dictionary<object, object>)schemas["Category"];
        var properties = (Dictionary<object, object>)category["properties"];

        return properties.Keys.Select(k => (string)k).ToHashSet();
    }
}
