using KartCategoryService.Domain.Attributes;

namespace KartCategoryService.Application.Common.Interfaces;

/// <summary>
/// Persistence abstraction for the ProductAttribute aggregate - one repository per aggregate root
/// (coding-standards.md DIP), mirroring ICategoryRepository's own shape.
/// </summary>
public interface IAttributeRepository
{
    /// <summary>categoryId null lists global attributes only; non-null lists that category's own attributes plus every global attribute (union) - api-contract.yaml listAttributes.</summary>
    Task<IReadOnlyList<ProductAttribute>> ListAsync(Guid? categoryId, bool includeDeprecated, CancellationToken cancellationToken);

    /// <summary>Null if the attribute does not exist or is deprecated - api-contract.yaml's uniform 404, matching ICategoryRepository.GetActiveByIdAsync.</summary>
    Task<ProductAttribute?> GetActiveByIdAsync(Guid attributeId, CancellationToken cancellationToken);

    Task AddAsync(ProductAttribute attribute, CancellationToken cancellationToken);
}
