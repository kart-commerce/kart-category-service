using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Domain.Categories;
using Microsoft.EntityFrameworkCore;

namespace KartCategoryService.Infrastructure.Persistence;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly CategoryDbContext _dbContext;

    public CategoryRepository(CategoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Category>> GetChildrenAsync(Guid? parentId, bool includeDeprecated, CancellationToken cancellationToken)
    {
        var query = _dbContext.Categories.AsNoTracking().Where(c => c.ParentId == parentId);

        if (!includeDeprecated)
        {
            query = query.Where(c => c.Status == CategoryStatus.Active);
        }

        return await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }
}
