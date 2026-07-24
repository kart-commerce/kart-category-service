using FluentAssertions;
using KartCategoryService.Domain.Categories;
using KartCategoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace KartCategoryService.IntegrationTests;

/// <summary>
/// Exercises CategoryRepository against a real PostgreSQL engine (via Testcontainers) - validates
/// the EF mapping, the applied migration, and idx_categories_parent_status's active/parentId
/// filtering (database-design.md), which an in-memory provider cannot verify (array column,
/// CHECK constraints, GIN index).
/// </summary>
public sealed class CategoryRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("kart_category_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private CategoryDbContext _dbContext = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<CategoryDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _dbContext = new CategoryDbContext(options);
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task GetChildrenAsync_ReturnsOnlyActiveChildren_OrderedByName_WhenIncludeDeprecatedIsFalse()
    {
        var electronics = Category.CreateRoot("Electronics", "system:test", DateTimeOffset.UtcNow).Value;
        var books = Category.CreateRoot("Books", "system:test", DateTimeOffset.UtcNow).Value;
        var archived = Category.CreateRoot("Archived Department", "system:test", DateTimeOffset.UtcNow).Value;
        archived.Deprecate("system:test", DateTimeOffset.UtcNow);

        _dbContext.Categories.AddRange(electronics, books, archived);
        await _dbContext.SaveChangesAsync();

        var repository = new CategoryRepository(_dbContext);
        var activeRoots = await repository.GetChildrenAsync(null, includeDeprecated: false, CancellationToken.None);

        activeRoots.Select(c => c.Name).Should().Equal("Books", "Electronics");
    }

    [Fact]
    public async Task GetChildrenAsync_IncludesDeprecated_WhenIncludeDeprecatedIsTrue()
    {
        var department = Category.CreateRoot("Home & Garden", "system:test", DateTimeOffset.UtcNow).Value;
        department.Deprecate("system:test", DateTimeOffset.UtcNow);
        _dbContext.Categories.Add(department);
        await _dbContext.SaveChangesAsync();

        var repository = new CategoryRepository(_dbContext);
        var all = await repository.GetChildrenAsync(null, includeDeprecated: true, CancellationToken.None);

        all.Should().Contain(c => c.Name == "Home & Garden" && c.Status == CategoryStatus.Deprecated);
    }
}
