using FluentAssertions;
using KartCategoryService.Domain.Categories;
using Xunit;

namespace KartCategoryService.UnitTests.Domain.Categories;

public sealed class CategoryDeprecateTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Deprecate_OnAnActiveCategory_SetsStatusAndRaisesDeprecatedEvent()
    {
        var category = Category.CreateRoot("Electronics", "system:test", Now).Value;
        category.ClearDomainEvents();

        var result = category.Deprecate("admin-principal", Now);

        result.IsSuccess.Should().BeTrue();
        category.Status.Should().Be(CategoryStatus.Deprecated);
        category.UpdatedBy.Should().Be("admin-principal");
        category.DomainEvents.Should().ContainSingle();
        ((CategoryUpdatedDomainEvent)category.DomainEvents.Single()).Operation.Should().Be(CategoryOperation.Deprecated);
    }

    [Fact]
    public void Deprecate_OnAnAlreadyDeprecatedCategory_ReturnsNotFound()
    {
        var category = Category.CreateRoot("Electronics", "system:test", Now).Value;
        category.Deprecate("admin-principal", Now);

        var result = category.Deprecate("admin-principal", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
    }

    [Fact]
    public void Deprecate_NeverPhysicallyRemovesTheCategoryId()
    {
        var category = Category.CreateRoot("Electronics", "system:test", Now).Value;
        var originalId = category.Id;

        category.Deprecate("admin-principal", Now);

        category.Id.Should().Be(originalId, "category_id is the shard key Product/Search key off downstream and must never be reassigned or reused");
    }
}
