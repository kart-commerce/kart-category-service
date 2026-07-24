using FluentValidation;

namespace KartCategoryService.Application.Features.CreateCategory;

/// <summary>api-contract.yaml createCategory requestBody: required: [name].</summary>
public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
    }
}
