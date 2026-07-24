using FluentValidation;

namespace KartCategoryService.Application.Features.RenameCategory;

/// <summary>api-contract.yaml renameCategory requestBody: required: [name].</summary>
public sealed class RenameCategoryCommandValidator : AbstractValidator<RenameCategoryCommand>
{
    public RenameCategoryCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
    }
}
