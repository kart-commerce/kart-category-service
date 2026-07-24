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

    public async Task<Category?> GetActiveByIdAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        return await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.Status == CategoryStatus.Active, cancellationToken);
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken)
    {
        await _dbContext.Categories.AddAsync(category, cancellationToken);
    }

    public async Task<Category?> GetForUpdateAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        return await _dbContext.Categories
            .FromSqlInterpolated($"SELECT * FROM categories WHERE category_id = {categoryId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Category>> GetDescendantsForUpdateAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        return await _dbContext.Categories
            .FromSqlInterpolated($"SELECT * FROM categories WHERE {categoryId} = ANY(ancestor_path) AND status = 'active' FOR UPDATE")
            .ToListAsync(cancellationToken);
    }
}
