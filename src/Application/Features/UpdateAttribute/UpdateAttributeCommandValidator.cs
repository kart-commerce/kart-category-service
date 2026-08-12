using FluentValidation;

namespace KartCategoryService.Application.Features.UpdateAttribute;

/// <summary>api-contract.yaml updateAttribute requestBody: required [name].</summary>
public sealed class UpdateAttributeCommandValidator : AbstractValidator<UpdateAttributeCommand>
{
    public UpdateAttributeCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
    }
}
