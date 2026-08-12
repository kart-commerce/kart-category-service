using FluentAssertions;
using KartCategoryService.Domain.Categories;
using KartCategoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace KartCategoryService.IntegrationTests;

/// <summary>
/// Verifies CategoryDbContext.SaveChangesAsync's domain-event-to-outbox conversion
/// (design-decisions.md, "Reliable Event Publication for CategoryUpdated") against real
/// PostgreSQL - the outbox row must land in the same transaction as the categories write.
/// </summary>
public sealed class CategoryOutboxTests : IAsyncLifetime
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
    public async Task SaveChangesAsync_OnCategoryCreate_WritesOneOutboxRowInTheSameTransaction()
    {
        var category = Category.CreateRoot("Electronics", "admin-principal", DateTimeOffset.UtcNow).Value;

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();

        category.DomainEvents.Should().BeEmpty("SaveChangesAsync must clear raised events once persisted");

        var outboxRows = await _dbContext.OutboxEvents.Where(e => e.CategoryId == category.Id).ToListAsync();
        outboxRows.Should().ContainSingle();
        var row = outboxRows[0];
        row.EventType.Should().Be("CategoryUpdated");
        row.CreatedBy.Should().Be("admin-principal");
        row.PublishedAt.Should().BeNull();
        row.Payload.Should().Contain("\"operation\":\"created\"").And.Contain(category.Id.ToString());
    }
}
