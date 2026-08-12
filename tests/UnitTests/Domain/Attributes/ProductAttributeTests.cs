using FluentAssertions;
using KartCategoryService.Domain.Attributes;
using Xunit;

namespace KartCategoryService.UnitTests.Domain.Attributes;

public sealed class ProductAttributeTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_TextAttributeWithNoValues_Succeeds()
    {
        var result = ProductAttribute.Create("Warranty period", null, AttributeDataType.Text, [], "system:test", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.DataType.Should().Be(AttributeDataType.Text);
        result.Value.Values.Should().BeEmpty();
        result.Value.DomainEvents.Should().ContainSingle();
        ((AttributeUpdatedDomainEvent)result.Value.DomainEvents.Single()).Operation.Should().Be(AttributeOperation.Created);
    }

    [Fact]
    public void Create_SelectAttributeWithNoValues_ReturnsValidationError()
    {
        var result = ProductAttribute.Create("Color", null, AttributeDataType.Select, [], "system:test", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation_error");
    }

    [Fact]
    public void Create_SelectAttributeWithValues_Succeeds()
    {
        var categoryId = Guid.NewGuid();
        var values = new List<(string Value, int DisplayOrder)> { ("Red", 0), ("Blue", 1) };

        var result = ProductAttribute.Create("Color", categoryId, AttributeDataType.Select, values, "system:test", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.CategoryId.Should().Be(categoryId);
        result.Value.Values.Should().HaveCount(2);
        result.Value.Values.Select(v => v.Value).Should().BeEquivalentTo("Red", "Blue");
    }

    [Fact]
    public void Create_TextAttributeWithValues_ReturnsValidationError()
    {
        var values = new List<(string Value, int DisplayOrder)> { ("anything", 0) };

        var result = ProductAttribute.Create("Warranty period", null, AttributeDataType.Text, values, "system:test", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation_error");
    }

    [Fact]
    public void Create_WithBlankName_ReturnsValidationError()
    {
        var result = ProductAttribute.Create("   ", null, AttributeDataType.Text, [], "system:test", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation_error");
    }

    [Fact]
    public void Update_ReplacesNameAndValuesAsOneOperation()
    {
        var values = new List<(string Value, int DisplayOrder)> { ("Red", 0) };
        var attribute = ProductAttribute.Create("Color", null, AttributeDataType.Select, values, "system:test", Now).Value;
        attribute.ClearDomainEvents();

        var newValues = new List<(string Value, int DisplayOrder)> { ("Red", 0), ("Blue", 1), ("Green", 2) };
        var result = attribute.Update("Primary Color", newValues, "admin-principal", Now);

        result.IsSuccess.Should().BeTrue();
        attribute.Name.Should().Be("Primary Color");
        attribute.Values.Should().HaveCount(3);
        attribute.UpdatedBy.Should().Be("admin-principal");
        ((AttributeUpdatedDomainEvent)attribute.DomainEvents.Single()).Operation.Should().Be(AttributeOperation.Updated);
    }

    [Fact]
    public void Deprecate_OnActiveAttribute_Succeeds()
    {
        var attribute = ProductAttribute.Create("Warranty period", null, AttributeDataType.Text, [], "system:test", Now).Value;
        attribute.ClearDomainEvents();

        var result = attribute.Deprecate("admin-principal", Now);

        result.IsSuccess.Should().BeTrue();
        attribute.Status.Should().Be(AttributeStatus.Deprecated);
        ((AttributeUpdatedDomainEvent)attribute.DomainEvents.Single()).Operation.Should().Be(AttributeOperation.Deprecated);
    }

    [Fact]
    public void Deprecate_OnAlreadyDeprecatedAttribute_ReturnsNotFound()
    {
        var attribute = ProductAttribute.Create("Warranty period", null, AttributeDataType.Text, [], "system:test", Now).Value;
        attribute.Deprecate("system:test", Now);

        var result = attribute.Deprecate("admin-principal", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
    }

    [Fact]
    public void Update_OnDeprecatedAttribute_ReturnsNotFound()
    {
        var attribute = ProductAttribute.Create("Warranty period", null, AttributeDataType.Text, [], "system:test", Now).Value;
        attribute.Deprecate("system:test", Now);

        var result = attribute.Update("New name", [], "admin-principal", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
    }
}
