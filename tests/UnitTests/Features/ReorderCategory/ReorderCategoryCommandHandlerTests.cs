using FluentAssertions;
using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Application.Features.ReorderCategory;
using KartCategoryService.Domain.Categories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KartCategoryService.UnitTests.Features.ReorderCategory;

public sealed class ReorderCategoryCommandHandlerTests
{
    private readonly Mock<ICategoryRepository> _repository = new();
    private readonly Mock<ICategoryCache> _cache = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly ReorderCategoryCommandHandler _handler;

    public ReorderCategoryCommandHandlerTests()
    {
        _currentPrincipal.Setup(p => p.ActingPrincipal).Returns("admin-service-principal");
        _handler = new ReorderCategoryCommandHandler(
            _repository.Object, _cache.Object, _unitOfWork.Object, _currentPrincipal.Object, TimeProvider.System,
            NullLogger<ReorderCategoryCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WithUnknownCategoryId_ReturnsNotFound()
    {
        var categoryId = Guid.NewGuid();
        _repository.Setup(r => r.GetActiveByIdAsync(categoryId, It.IsAny<CancellationToken>())).ReturnsAsync((Category?)null);

        var result = await _handler.Handle(new ReorderCategoryCommand(categoryId, 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task Handle_WithValidCategory_ReordersAndRefreshesParentCache()
    {
        var category = Category.CreateRoot("Electronics", "system:test", DateTimeOffset.UtcNow).Value;
        _repository.Setup(r => r.GetActiveByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        _repository
            .Setup(r => r.GetChildrenAsync(null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category> { category });

        var result = await _handler.Handle(new ReorderCategoryCommand(category.Id, 4), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayOrder.Should().Be(4);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(
            c => c.SetChildrenAsync(null, It.Is<IReadOnlyList<CategoryDto>>(dtos => dtos.Count == 1), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
