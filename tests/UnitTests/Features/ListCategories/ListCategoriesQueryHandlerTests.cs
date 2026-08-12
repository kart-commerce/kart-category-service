using FluentAssertions;
using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Application.Features.ListCategories;
using KartCategoryService.Domain.Categories;
using Moq;
using Xunit;

namespace KartCategoryService.UnitTests.Features.ListCategories;

public sealed class ListCategoriesQueryHandlerTests
{
    private readonly Mock<ICategoryRepository> _repository = new();
    private readonly Mock<ICategoryCache> _cache = new();
    private readonly ListCategoriesQueryHandler _handler;

    public ListCategoriesQueryHandlerTests()
    {
        _handler = new ListCategoriesQueryHandler(_repository.Object, _cache.Object);
    }

    private static Category NewRoot(string name) =>
        Category.CreateRoot(name, "system:test", DateTimeOffset.UtcNow).Value;

    [Fact]
    public async Task Handle_WithIncludeDeprecatedTrue_BypassesCacheAndReadsRepositoryDirectly()
    {
        var parentId = Guid.NewGuid();
        var categories = new List<Category> { NewRoot("Electronics") };
        _repository
            .Setup(r => r.GetChildrenAsync(parentId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);

        var result = await _handler.Handle(new ListCategoriesQuery(parentId, IncludeDeprecated: true), CancellationToken.None);

        result.Should().ContainSingle(c => c.Name == "Electronics");
        _cache.Verify(c => c.GetChildrenAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.SetChildrenAsync(It.IsAny<Guid?>(), It.IsAny<IReadOnlyList<CategoryDto>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OnCacheHit_ReturnsCachedValueWithoutTouchingRepository()
    {
        var cached = new List<CategoryDto> { new(Guid.NewGuid(), "Cached", null, Array.Empty<Guid>(), 1, 0, "active") };
        _cache
            .Setup(c => c.GetChildrenAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var result = await _handler.Handle(new ListCategoriesQuery(null, IncludeDeprecated: false), CancellationToken.None);

        result.Should().BeSameAs(cached);
        _repository.Verify(r => r.GetChildrenAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OnCacheMiss_FallsBackToRepositoryAndWarmsCache()
    {
        _cache
            .Setup(c => c.GetChildrenAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CategoryDto>?)null);
        var categories = new List<Category> { NewRoot("Books"), NewRoot("Toys") };
        _repository
            .Setup(r => r.GetChildrenAsync(null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);

        var result = await _handler.Handle(new ListCategoriesQuery(null, IncludeDeprecated: false), CancellationToken.None);

        result.Should().HaveCount(2);
        _cache.Verify(
            c => c.SetChildrenAsync(null, It.Is<IReadOnlyList<CategoryDto>>(dtos => dtos.Count == 2), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
