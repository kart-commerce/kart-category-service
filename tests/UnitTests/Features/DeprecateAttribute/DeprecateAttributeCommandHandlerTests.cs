using FluentAssertions;
using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Features.DeprecateAttribute;
using KartCategoryService.Domain.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KartCategoryService.UnitTests.Features.DeprecateAttribute;

public sealed class DeprecateAttributeCommandHandlerTests
{
    private readonly Mock<IAttributeRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly DeprecateAttributeCommandHandler _handler;

    public DeprecateAttributeCommandHandlerTests()
    {
        _currentPrincipal.Setup(p => p.ActingPrincipal).Returns("admin-service-principal");
        _handler = new DeprecateAttributeCommandHandler(
            _repository.Object, _unitOfWork.Object, _currentPrincipal.Object, TimeProvider.System,
            NullLogger<DeprecateAttributeCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WithUnknownAttributeId_ReturnsNotFound()
    {
        var attributeId = Guid.NewGuid();
        _repository.Setup(r => r.GetActiveByIdAsync(attributeId, It.IsAny<CancellationToken>())).ReturnsAsync((ProductAttribute?)null);

        var result = await _handler.Handle(new DeprecateAttributeCommand(attributeId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task Handle_WithActiveAttribute_DeprecatesAndPersists()
    {
        var attribute = ProductAttribute.Create("Warranty period", null, AttributeDataType.Text, [], "system:test", DateTimeOffset.UtcNow).Value;
        _repository.Setup(r => r.GetActiveByIdAsync(attribute.Id, It.IsAny<CancellationToken>())).ReturnsAsync(attribute);

        var result = await _handler.Handle(new DeprecateAttributeCommand(attribute.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        attribute.Status.Should().Be(AttributeStatus.Deprecated);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
