using FluentAssertions;
using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Features.CreateAttribute;
using KartCategoryService.Domain.Attributes;
using KartCategoryService.Domain.Categories;
using Moq;
using Xunit;

namespace KartCategoryService.UnitTests.Features.CreateAttribute;

public sealed class CreateAttributeCommandHandlerTests
{
    private readonly Mock<IAttributeRepository> _attributeRepository = new();
    private readonly Mock<ICategoryRepository> _categoryRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly CreateAttributeCommandHandler _handler;

    public CreateAttributeCommandHandlerTests()
    {
        _currentPrincipal.Setup(p => p.ActingPrincipal).Returns("admin-service-principal");
        _handler = new CreateAttributeCommandHandler(
            _attributeRepository.Object, _categoryRepository.Object, _unitOfWork.Object, _currentPrincipal.Object, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithGlobalTextAttribute_CreatesAndPersists()
    {
        var command = new CreateAttributeCommand("Warranty period", null, "text", []);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Warranty period");
        result.Value.CategoryId.Should().BeNull();
        _attributeRepository.Verify(r => r.AddAsync(It.IsAny<ProductAttribute>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _categoryRepository.Verify(r => r.GetActiveByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithUnknownCategoryId_ReturnsNotFound()
    {
        var categoryId = Guid.NewGuid();
        _categoryRepository.Setup(r => r.GetActiveByIdAsync(categoryId, It.IsAny<CancellationToken>())).ReturnsAsync((Category?)null);
        var command = new CreateAttributeCommand("Color", categoryId, "select", [new CreateAttributeValueRequest("Red", 0)]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
        _attributeRepository.Verify(r => r.AddAsync(It.IsAny<ProductAttribute>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithScopedSelectAttribute_CreatesUnderExistingCategory()
    {
        var category = Category.CreateRoot("Electronics", "system:test", DateTimeOffset.UtcNow).Value;
        _categoryRepository.Setup(r => r.GetActiveByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        var command = new CreateAttributeCommand("Color", category.Id, "select", [new CreateAttributeValueRequest("Red", 0), new CreateAttributeValueRequest("Blue", 1)]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CategoryId.Should().Be(category.Id);
        result.Value.Values.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithSelectAttributeAndNoValues_ReturnsValidationError()
    {
        var command = new CreateAttributeCommand("Color", null, "select", []);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation_error");
    }
}
