using FluentAssertions;
using KartCategoryService.Domain.Categories;
using Xunit;

namespace KartCategoryService.UnitTests.Domain.Categories;

public sealed class CategoryRenameTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Rename_WithValidName_UpdatesNameAndRaisesRenamedEvent()
    {
        var category = Category.CreateRoot("Electronics", "system:test", Now).Value;
        category.ClearDomainEvents();

        var result = category.Rename("Consumer Electronics", "admin-principal", Now);

        result.IsSuccess.Should().BeTrue();
        category.Name.Should().Be("Consumer Electronics");
        category.UpdatedBy.Should().Be("admin-principal");
        category.DomainEvents.Should().ContainSingle();
        ((CategoryUpdatedDomainEvent)category.DomainEvents.Single()).Operation.Should().Be(CategoryOperation.Renamed);
    }

    [Fact]
    public void Rename_WithBlankName_ReturnsValidationError()
    {
        var category = Category.CreateRoot("Electronics", "system:test", Now).Value;

        var result = category.Rename("   ", "admin-principal", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation_error");
    }

    [Fact]
    public void Rename_OnADeprecatedCategory_ReturnsNotFound()
    {
        var category = Category.CreateRoot("Electronics", "system:test", Now).Value;
        category.Deprecate("system:test", Now);

        var result = category.Rename("New Name", "admin-principal", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
    }
}
