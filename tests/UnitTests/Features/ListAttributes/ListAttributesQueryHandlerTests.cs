using FluentAssertions;
using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Features.ListAttributes;
using KartCategoryService.Domain.Attributes;
using Moq;
using Xunit;

namespace KartCategoryService.UnitTests.Features.ListAttributes;

public sealed class ListAttributesQueryHandlerTests
{
    private readonly Mock<IAttributeRepository> _repository = new();
    private readonly ListAttributesQueryHandler _handler;

    public ListAttributesQueryHandlerTests()
    {
        _handler = new ListAttributesQueryHandler(_repository.Object);
    }

    [Fact]
    public async Task Handle_DelegatesToRepositoryAndMapsToDto()
    {
        var categoryId = Guid.NewGuid();
        var attribute = ProductAttribute.Create("Color", categoryId, AttributeDataType.Select, [("Red", 0)], "system:test", DateTimeOffset.UtcNow).Value;
        _repository.Setup(r => r.ListAsync(categoryId, false, It.IsAny<CancellationToken>())).ReturnsAsync(new List<ProductAttribute> { attribute });

        var result = await _handler.Handle(new ListAttributesQuery(categoryId, false), CancellationToken.None);

        result.Should().ContainSingle(a => a.Name == "Color" && a.Values.Count == 1);
    }
}
