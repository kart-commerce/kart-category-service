using KartCategoryService.Application.Common.Interfaces;

namespace KartCategoryService.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly CategoryDbContext _dbContext;

    public EfUnitOfWork(CategoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
