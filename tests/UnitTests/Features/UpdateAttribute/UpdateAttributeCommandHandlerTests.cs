using FluentAssertions;
using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Features.CreateAttribute;
using KartCategoryService.Application.Features.UpdateAttribute;
using KartCategoryService.Domain.Attributes;
using Moq;
using Xunit;

namespace KartCategoryService.UnitTests.Features.UpdateAttribute;

public sealed class UpdateAttributeCommandHandlerTests
{
    private readonly Mock<IAttributeRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly UpdateAttributeCommandHandler _handler;

    public UpdateAttributeCommandHandlerTests()
    {
        _currentPrincipal.Setup(p => p.ActingPrincipal).Returns("admin-service-principal");
        _handler = new UpdateAttributeCommandHandler(_repository.Object, _unitOfWork.Object, _currentPrincipal.Object, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithUnknownAttributeId_ReturnsNotFound()
    {
        var attributeId = Guid.NewGuid();
        _repository.Setup(r => r.GetActiveByIdAsync(attributeId, It.IsAny<CancellationToken>())).ReturnsAsync((ProductAttribute?)null);

        var result = await _handler.Handle(new UpdateAttributeCommand(attributeId, "New name", []), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task Handle_WithValidAttribute_UpdatesAndPersists()
    {
        var attribute = ProductAttribute.Create("Color", null, AttributeDataType.Select, [("Red", 0)], "system:test", DateTimeOffset.UtcNow).Value;
        _repository.Setup(r => r.GetActiveByIdAsync(attribute.Id, It.IsAny<CancellationToken>())).ReturnsAsync(attribute);

        var command = new UpdateAttributeCommand(attribute.Id, "Primary Color", [new CreateAttributeValueRequest("Red", 0), new CreateAttributeValueRequest("Blue", 1)]);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Primary Color");
        result.Value.Values.Should().HaveCount(2);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
