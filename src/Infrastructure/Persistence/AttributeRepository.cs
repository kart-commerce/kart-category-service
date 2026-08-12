using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Domain.Attributes;
using Microsoft.EntityFrameworkCore;

namespace KartCategoryService.Infrastructure.Persistence;

public sealed class AttributeRepository : IAttributeRepository
{
    private readonly CategoryDbContext _dbContext;

    public AttributeRepository(CategoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ProductAttribute>> ListAsync(Guid? categoryId, bool includeDeprecated, CancellationToken cancellationToken)
    {
        var query = _dbContext.Attributes.AsNoTracking().AsQueryable();

        query = categoryId is { } id
            ? query.Where(a => a.CategoryId == id || a.CategoryId == null)
            : query;

        if (!includeDeprecated)
        {
            query = query.Where(a => a.Status == AttributeStatus.Active);
        }

        return await query.OrderBy(a => a.Name).ToListAsync(cancellationToken);
    }

    public async Task<ProductAttribute?> GetActiveByIdAsync(Guid attributeId, CancellationToken cancellationToken)
    {
        return await _dbContext.Attributes
            .FirstOrDefaultAsync(a => a.Id == attributeId && a.Status == AttributeStatus.Active, cancellationToken);
    }

    public async Task AddAsync(ProductAttribute attribute, CancellationToken cancellationToken)
    {
        await _dbContext.Attributes.AddAsync(attribute, cancellationToken);
    }
}
