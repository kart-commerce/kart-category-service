using FluentAssertions;
using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Application.Features.CreateCategory;
using KartCategoryService.Domain.Categories;
using Moq;
using Xunit;

namespace KartCategoryService.UnitTests.Features.CreateCategory;

public sealed class CreateCategoryCommandHandlerTests
{
    private readonly Mock<ICategoryRepository> _repository = new();
    private readonly Mock<ICategoryCache> _cache = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly CreateCategoryCommandHandler _handler;

    public CreateCategoryCommandHandlerTests()
    {
        _currentPrincipal.Setup(p => p.ActingPrincipal).Returns("admin-service-principal");
        _repository
            .Setup(r => r.GetChildrenAsync(It.IsAny<Guid?>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category>());

        _handler = new CreateCategoryCommandHandler(
            _repository.Object,
            _cache.Object,
            _unitOfWork.Object,
            _currentPrincipal.Object,
            TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithNoParentId_CreatesADepth1Category()
    {
        var command = new CreateCategoryCommand("Electronics", ParentId: null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Electronics");
        result.Value.Depth.Should().Be(1);
        result.Value.ParentId.Should().BeNull();
        _repository.Verify(r => r.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithUnknownParentId_ReturnsNotFound()
    {
        var parentId = Guid.NewGuid();
        _repository
            .Setup(r => r.GetActiveByIdAsync(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var result = await _handler.Handle(new CreateCategoryCommand("Phones", parentId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
        _repository.Verify(r => r.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenParentAlreadyAtMaxDepth_ReturnsMaxDepthExceeded()
    {
        var now = DateTimeOffset.UtcNow;
        var department = Category.CreateRoot("Dept", "system:test", now).Value;
        var category = Category.CreateChild("Category", department, "system:test", now).Value;
        var subcategory = Category.CreateChild("Subcategory", category, "system:test", now).Value;
        var subsubcategory = Category.CreateChild("SubSubcategory", subcategory, "system:test", now).Value;

        _repository
            .Setup(r => r.GetActiveByIdAsync(subsubcategory.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subsubcategory);

        var result = await _handler.Handle(new CreateCategoryCommand("TooDeep", subsubcategory.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("max_depth_exceeded");
    }

    [Fact]
    public async Task Handle_OnSuccess_RefreshesParentChildrenCache()
    {
        var result = await _handler.Handle(new CreateCategoryCommand("Books", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _cache.Verify(
            c => c.SetChildrenAsync(null, It.Is<IReadOnlyList<CategoryDto>>(_ => true), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
