using FluentAssertions;
using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Application.Features.MoveCategory;
using KartCategoryService.Domain.Categories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KartCategoryService.UnitTests.Features.MoveCategory;

public sealed class MoveCategoryCommandHandlerTests
{
    private readonly Mock<ICategoryRepository> _repository = new();
    private readonly Mock<ICategoryCache> _cache = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly MoveCategoryCommandHandler _handler;

    public MoveCategoryCommandHandlerTests()
    {
        _currentPrincipal.Setup(p => p.ActingPrincipal).Returns("admin-service-principal");
        _repository
            .Setup(r => r.GetChildrenAsync(It.IsAny<Guid?>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category>());

        _handler = new MoveCategoryCommandHandler(
            _repository.Object, _cache.Object, _unitOfWork.Object, _currentPrincipal.Object, TimeProvider.System,
            NullLogger<MoveCategoryCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WithUnknownCategoryId_ReturnsNotFoundAndRollsBack()
    {
        var categoryId = Guid.NewGuid();
        _repository.Setup(r => r.GetForUpdateAsync(categoryId, It.IsAny<CancellationToken>())).ReturnsAsync((Category?)null);

        var result = await _handler.Handle(new MoveCategoryCommand(categoryId, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
        _unitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithUnknownNewParentId_ReturnsNotFoundAndRollsBack()
    {
        var now = DateTimeOffset.UtcNow;
        var category = Category.CreateRoot("Electronics", "system:test", now).Value;
        var newParentId = Guid.NewGuid();

        _repository.Setup(r => r.GetForUpdateAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        _repository.Setup(r => r.GetForUpdateAsync(newParentId, It.IsAny<CancellationToken>())).ReturnsAsync((Category?)null);

        var result = await _handler.Handle(new MoveCategoryCommand(category.Id, newParentId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
        _unitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidMove_CommitsAndRefreshesOldAndNewParentCaches()
    {
        var now = DateTimeOffset.UtcNow;
        var oldParent = Category.CreateRoot("OldParent", "system:test", now).Value;
        var newParent = Category.CreateRoot("NewParent", "system:test", now).Value;
        var category = Category.CreateChild("Category", oldParent, "system:test", now).Value;

        _repository.Setup(r => r.GetForUpdateAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        _repository.Setup(r => r.GetForUpdateAsync(newParent.Id, It.IsAny<CancellationToken>())).ReturnsAsync(newParent);
        _repository
            .Setup(r => r.GetDescendantsForUpdateAsync(category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category>());

        var result = await _handler.Handle(new MoveCategoryCommand(category.Id, newParent.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ParentId.Should().Be(newParent.Id);
        _unitOfWork.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);

        // old parent's children list loses this category; new parent's children list gains it.
        _cache.Verify(c => c.SetChildrenAsync(oldParent.Id, It.IsAny<IReadOnlyList<CategoryDto>>(), It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.SetChildrenAsync(newParent.Id, It.IsAny<IReadOnlyList<CategoryDto>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CircularMove_ReturnsFailureAndDoesNotSave()
    {
        var now = DateTimeOffset.UtcNow;
        var department = Category.CreateRoot("Department", "system:test", now).Value;
        var category = Category.CreateChild("Category", department, "system:test", now).Value;
        var subcategory = Category.CreateChild("Subcategory", category, "system:test", now).Value;

        _repository.Setup(r => r.GetForUpdateAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        _repository.Setup(r => r.GetForUpdateAsync(subcategory.Id, It.IsAny<CancellationToken>())).ReturnsAsync(subcategory);
        _repository
            .Setup(r => r.GetDescendantsForUpdateAsync(category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category> { subcategory });

        var result = await _handler.Handle(new MoveCategoryCommand(category.Id, subcategory.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("circular_reference");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
