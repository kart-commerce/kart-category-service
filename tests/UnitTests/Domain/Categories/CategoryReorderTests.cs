using FluentAssertions;
using KartCategoryService.Domain.Categories;
using Xunit;

namespace KartCategoryService.UnitTests.Domain.Categories;

public sealed class CategoryReorderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Reorder_WithNonNegativeDisplayOrder_UpdatesDisplayOrderAndRaisesReorderedEvent()
    {
        var category = Category.CreateRoot("Electronics", "system:test", Now).Value;
        category.ClearDomainEvents();

        var result = category.Reorder(3, "admin-principal", Now);

        result.IsSuccess.Should().BeTrue();
        category.DisplayOrder.Should().Be(3);
        category.UpdatedBy.Should().Be("admin-principal");
        category.DomainEvents.Should().ContainSingle();
        ((CategoryUpdatedDomainEvent)category.DomainEvents.Single()).Operation.Should().Be(CategoryOperation.Reordered);
    }

    [Fact]
    public void Reorder_WithNegativeDisplayOrder_ReturnsValidationError()
    {
        var category = Category.CreateRoot("Electronics", "system:test", Now).Value;

        var result = category.Reorder(-1, "admin-principal", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation_error");
    }

    [Fact]
    public void Reorder_OnADeprecatedCategory_ReturnsNotFound()
    {
        var category = Category.CreateRoot("Electronics", "system:test", Now).Value;
        category.Deprecate("system:test", Now);

        var result = category.Reorder(2, "admin-principal", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
    }

    [Fact]
    public void Reorder_DoesNotTouchParentOrAncestorPath()
    {
        var parent = Category.CreateRoot("Electronics", "system:test", Now).Value;
        var child = Category.CreateChild("Laptops", parent, "system:test", Now).Value;

        child.Reorder(5, "admin-principal", Now);

        child.ParentId.Should().Be(parent.Id);
        child.AncestorPath.Should().BeEquivalentTo(new[] { parent.Id });
        child.Depth.Should().Be(2);
    }
}
