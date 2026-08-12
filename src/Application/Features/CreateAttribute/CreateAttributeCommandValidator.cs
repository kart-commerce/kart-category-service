using FluentValidation;
using KartCategoryService.Domain.Attributes;

namespace KartCategoryService.Application.Features.CreateAttribute;

/// <summary>api-contract.yaml createAttribute requestBody: required [name, dataType]; dataType must be one of AttributeDataType's members.</summary>
public sealed class CreateAttributeCommandValidator : AbstractValidator<CreateAttributeCommand>
{
    public CreateAttributeCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
        RuleFor(c => c.DataType)
            .Must(dataType => Enum.TryParse<AttributeDataType>(dataType, ignoreCase: true, out _))
            .WithMessage($"dataType must be one of: {string.Join(", ", Enum.GetNames<AttributeDataType>())}.");
    }
}
