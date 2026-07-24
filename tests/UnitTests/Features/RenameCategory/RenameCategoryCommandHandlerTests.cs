using FluentAssertions;
using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Application.Features.RenameCategory;
using KartCategoryService.Domain.Categories;
using Moq;
using Xunit;

namespace KartCategoryService.UnitTests.Features.RenameCategory;

public sealed class RenameCategoryCommandHandlerTests
{
    private readonly Mock<ICategoryRepository> _repository = new();
    private readonly Mock<ICategoryCache> _cache = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly RenameCategoryCommandHandler _handler;

    public RenameCategoryCommandHandlerTests()
    {
        _currentPrincipal.Setup(p => p.ActingPrincipal).Returns("admin-service-principal");
        _handler = new RenameCategoryCommandHandler(
            _repository.Object, _cache.Object, _unitOfWork.Object, _currentPrincipal.Object, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithUnknownCategoryId_ReturnsNotFound()
    {
        var categoryId = Guid.NewGuid();
        _repository.Setup(r => r.GetActiveByIdAsync(categoryId, It.IsAny<CancellationToken>())).ReturnsAsync((Category?)null);

        var result = await _handler.Handle(new RenameCategoryCommand(categoryId, "New Name"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task Handle_WithValidCategory_RenamesAndRefreshesParentCache()
    {
        var category = Category.CreateRoot("Electronics", "system:test", DateTimeOffset.UtcNow).Value;
        _repository.Setup(r => r.GetActiveByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        _repository
            .Setup(r => r.GetChildrenAsync(null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category> { category });

        var result = await _handler.Handle(new RenameCategoryCommand(category.Id, "Consumer Electronics"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Consumer Electronics");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(
            c => c.SetChildrenAsync(null, It.Is<IReadOnlyList<CategoryDto>>(dtos => dtos.Count == 1), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
