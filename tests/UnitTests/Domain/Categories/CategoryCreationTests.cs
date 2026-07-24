using FluentAssertions;
using KartCategoryService.Domain.Categories;
using Xunit;

namespace KartCategoryService.UnitTests.Domain.Categories;

public sealed class CategoryCreationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void CreateRoot_WithBlankName_ReturnsValidationError()
    {
        var result = Category.CreateRoot("   ", "system:test", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation_error");
    }

    [Fact]
    public void CreateRoot_ProducesADepth1CategoryAndRaisesCreatedEvent()
    {
        var result = Category.CreateRoot("Electronics", "admin-principal", Now);

        result.IsSuccess.Should().BeTrue();
        var category = result.Value;
        category.Depth.Should().Be(1);
        category.ParentId.Should().BeNull();
        category.AncestorPath.Should().BeEmpty();
        category.Status.Should().Be(CategoryStatus.Active);
        category.CreatedBy.Should().Be("admin-principal");
        category.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<CategoryUpdatedDomainEvent>();
        ((CategoryUpdatedDomainEvent)category.DomainEvents.Single()).Operation.Should().Be(CategoryOperation.Created);
    }

    [Fact]
    public void CreateChild_UnderActiveParent_ComputesAncestorPathAndDepth()
    {
        var department = Category.CreateRoot("Department", "system:test", Now).Value;

        var result = Category.CreateChild("Category", department, "admin-principal", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.ParentId.Should().Be(department.Id);
        result.Value.AncestorPath.Should().Equal(department.Id);
        result.Value.Depth.Should().Be(2);
    }

    [Fact]
    public void CreateChild_UnderDeprecatedParent_ReturnsNotFound()
    {
        var department = Category.CreateRoot("Department", "system:test", Now).Value;
        department.Deprecate("system:test", Now);

        var result = Category.CreateChild("Category", department, "admin-principal", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
    }

    [Fact]
    public void CreateChild_AtDepth4Parent_ReturnsMaxDepthExceeded()
    {
        var department = Category.CreateRoot("Department", "system:test", Now).Value;
        var category = Category.CreateChild("Category", department, "system:test", Now).Value;
        var subcategory = Category.CreateChild("Subcategory", category, "system:test", Now).Value;
        var subsubcategory = Category.CreateChild("SubSubcategory", subcategory, "system:test", Now).Value;
        subsubcategory.Depth.Should().Be(4);

        var result = Category.CreateChild("TooDeep", subsubcategory, "admin-principal", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("max_depth_exceeded");
    }
}
