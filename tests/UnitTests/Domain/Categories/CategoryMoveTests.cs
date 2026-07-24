using FluentAssertions;
using KartCategoryService.Domain.Categories;
using Xunit;

namespace KartCategoryService.UnitTests.Domain.Categories;

public sealed class CategoryMoveTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Category Root(string name) => Category.CreateRoot(name, "system:test", Now).Value;

    private static Category Child(string name, Category parent) =>
        Category.CreateChild(name, parent, "system:test", Now).Value;

    [Fact]
    public void MoveTo_OntoOwnDescendant_ReturnsCircularReference()
    {
        var department = Root("Department");
        var category = Child("Category", department);
        var subcategory = Child("Subcategory", category);

        var result = category.MoveTo(subcategory, descendants: new[] { subcategory }, "admin-principal", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("circular_reference");
    }

    [Fact]
    public void MoveTo_OntoItself_ReturnsCircularReference()
    {
        var category = Root("Category");

        var result = category.MoveTo(category, Array.Empty<Category>(), "admin-principal", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("circular_reference");
    }

    [Fact]
    public void MoveTo_WouldPushADescendantPastMaxDepth_ReturnsMaxDepthExceeded()
    {
        // department(1) -> categoryA(2) -> sub(3) -> subsub(4); categoryB(2) is the move target.
        var department = Root("Department");
        var categoryA = Child("CategoryA", department);
        var sub = Child("Sub", categoryA);
        var subsub = Child("SubSub", sub);
        var categoryB = Child("CategoryB", department);
        var categoryBChild = Child("CategoryBChild", categoryB); // depth 3

        // Moving `sub` (currently depth 3, carrying subsub at depth 4) under categoryBChild (depth 3)
        // would push subsub to depth 5 - over the limit.
        var result = sub.MoveTo(categoryBChild, new[] { subsub }, "admin-principal", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("max_depth_exceeded");
    }

    [Fact]
    public void MoveTo_ToTopLevel_ClearsParentAndAncestorPath()
    {
        var department = Root("Department");
        var category = Child("Category", department);

        var result = category.MoveTo(newParent: null, Array.Empty<Category>(), "admin-principal", Now);

        result.IsSuccess.Should().BeTrue();
        category.ParentId.Should().BeNull();
        category.AncestorPath.Should().BeEmpty();
        category.Depth.Should().Be(1);
    }

    [Fact]
    public void MoveTo_ValidMove_RecomputesDescendantAncestorPathsAndRaisesExactlyOneEvent()
    {
        var departmentA = Root("DepartmentA");
        var categoryA = Child("CategoryA", departmentA);
        var subA = Child("SubA", categoryA); // depth 3, ancestorPath [departmentA, categoryA]
        var departmentB = Root("DepartmentB");
        categoryA.ClearDomainEvents();
        subA.ClearDomainEvents();

        var result = categoryA.MoveTo(departmentB, new[] { subA }, "admin-principal", Now);

        result.IsSuccess.Should().BeTrue();
        categoryA.ParentId.Should().Be(departmentB.Id);
        categoryA.AncestorPath.Should().Equal(departmentB.Id);
        categoryA.Depth.Should().Be(2);

        // subA's own immediate parent (categoryA) is unchanged; only its ancestor chain shifted.
        subA.ParentId.Should().Be(categoryA.Id);
        subA.AncestorPath.Should().Equal(departmentB.Id, categoryA.Id);
        subA.Depth.Should().Be(3);

        categoryA.DomainEvents.Should().ContainSingle();
        subA.DomainEvents.Should().BeEmpty("only the moved subtree's root raises CategoryUpdated, per edge-cases.md");
        ((CategoryUpdatedDomainEvent)categoryA.DomainEvents.Single()).Operation.Should().Be(CategoryOperation.Moved);
    }
}
