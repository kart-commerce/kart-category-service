using FluentAssertions;
using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Application.Features.DeprecateCategory;
using KartCategoryService.Domain.Categories;
using Moq;
using Xunit;

namespace KartCategoryService.UnitTests.Features.DeprecateCategory;

public sealed class DeprecateCategoryCommandHandlerTests
{
    private readonly Mock<ICategoryRepository> _repository = new();
    private readonly Mock<ICategoryCache> _cache = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly DeprecateCategoryCommandHandler _handler;

    public DeprecateCategoryCommandHandlerTests()
    {
        _currentPrincipal.Setup(p => p.ActingPrincipal).Returns("admin-service-principal");
        _handler = new DeprecateCategoryCommandHandler(
            _repository.Object, _cache.Object, _unitOfWork.Object, _currentPrincipal.Object, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithUnknownCategoryId_ReturnsNotFound()
    {
        var categoryId = Guid.NewGuid();
        _repository.Setup(r => r.GetActiveByIdAsync(categoryId, It.IsAny<CancellationToken>())).ReturnsAsync((Category?)null);

        var result = await _handler.Handle(new DeprecateCategoryCommand(categoryId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithActiveCategory_DeprecatesAndRefreshesParentCache()
    {
        var category = Category.CreateRoot("Electronics", "system:test", DateTimeOffset.UtcNow).Value;
        _repository.Setup(r => r.GetActiveByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        _repository
            .Setup(r => r.GetChildrenAsync(null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category>());

        var result = await _handler.Handle(new DeprecateCategoryCommand(category.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        category.Status.Should().Be(CategoryStatus.Deprecated);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(
            c => c.SetChildrenAsync(null, It.Is<IReadOnlyList<CategoryDto>>(dtos => dtos.Count == 0), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
