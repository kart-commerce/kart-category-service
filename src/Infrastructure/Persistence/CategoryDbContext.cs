using KartCategoryService.Domain.Categories;
using KartCategoryService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace KartCategoryService.Infrastructure.Persistence;

public sealed class CategoryDbContext : DbContext
{
    public CategoryDbContext(DbContextOptions<CategoryDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CategoryConfiguration());
    }
}
