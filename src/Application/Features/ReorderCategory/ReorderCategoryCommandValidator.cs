using FluentValidation;

namespace KartCategoryService.Application.Features.ReorderCategory;

/// <summary>api-contract.yaml reorderCategory requestBody: displayOrder must be zero or a positive integer.</summary>
public sealed class ReorderCategoryCommandValidator : AbstractValidator<ReorderCategoryCommand>
{
    public ReorderCategoryCommandValidator()
    {
        RuleFor(c => c.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}
