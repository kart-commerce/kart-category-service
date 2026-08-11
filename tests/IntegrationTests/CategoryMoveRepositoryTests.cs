using FluentAssertions;
using KartCategoryService.Domain.Categories;
using KartCategoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace KartCategoryService.IntegrationTests;

/// <summary>
/// Verifies the raw "SELECT ... FOR UPDATE" queries CategoryRepository issues for MoveCategory
/// (design-decisions.md, "Concurrency Control for Hierarchy Mutations") actually execute and map
/// correctly against real PostgreSQL - an in-memory provider can't validate raw SQL/array-contains.
/// </summary>
public sealed class CategoryMoveRepositoryTests : IAsyncLifetime
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

        _dbContext = new CategoryDbContext(options, NullLogger<CategoryDbContext>.Instance);
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task GetForUpdateAsync_And_GetDescendantsForUpdateAsync_ExecuteAndMoveTheSubtree()
    {
        var now = DateTimeOffset.UtcNow;
        var departmentA = Category.CreateRoot("DepartmentA", "system:test", now).Value;
        var categoryA = Category.CreateChild("CategoryA", departmentA, "system:test", now).Value;
        var subA = Category.CreateChild("SubA", categoryA, "system:test", now).Value;
        var departmentB = Category.CreateRoot("DepartmentB", "system:test", now).Value;
        _dbContext.Categories.AddRange(departmentA, categoryA, subA, departmentB);
        await _dbContext.SaveChangesAsync();

        var repository = new CategoryRepository(_dbContext);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        var lockedTarget = await repository.GetForUpdateAsync(categoryA.Id, CancellationToken.None);
        var lockedNewParent = await repository.GetForUpdateAsync(departmentB.Id, CancellationToken.None);
        var lockedDescendants = await repository.GetDescendantsForUpdateAsync(categoryA.Id, CancellationToken.None);

        lockedTarget.Should().NotBeNull();
        lockedNewParent.Should().NotBeNull();
        lockedDescendants.Should().ContainSingle(d => d.Id == subA.Id);

        var moveResult = lockedTarget!.MoveTo(lockedNewParent, lockedDescendants, "admin-principal", now);
        moveResult.IsSuccess.Should().BeTrue();

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        var reloaded = await _dbContext.Categories.AsNoTracking().FirstAsync(c => c.Id == subA.Id);
        reloaded.AncestorPath.Should().Equal(departmentB.Id, categoryA.Id);
        reloaded.Depth.Should().Be(3);
    }
}
