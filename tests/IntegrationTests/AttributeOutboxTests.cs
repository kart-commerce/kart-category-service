using FluentAssertions;
using KartCategoryService.Domain.Attributes;
using KartCategoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace KartCategoryService.IntegrationTests;

/// <summary>
/// Verifies CategoryDbContext.SaveChangesAsync's domain-event-to-outbox conversion for the new
/// ProductAttribute aggregate against real PostgreSQL - both that the owned `attribute_values`
/// collection round-trips through EF's backing-field-access mapping (AttributeConfiguration) and
/// that the outbox row lands in the same transaction as the attributes/attribute_values write,
/// mirroring CategoryOutboxTests' own shape for the platform's second aggregate.
/// </summary>
public sealed class AttributeOutboxTests : IAsyncLifetime
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
    public async Task SaveChangesAsync_OnAttributeCreateWithValues_PersistsOwnedValuesAndOneOutboxRow()
    {
        var values = new List<(string Value, int DisplayOrder)> { ("Red", 0), ("Blue", 1) };
        var attribute = ProductAttribute.Create("Color", null, AttributeDataType.Select, values, "admin-principal", DateTimeOffset.UtcNow).Value;

        _dbContext.Attributes.Add(attribute);
        await _dbContext.SaveChangesAsync();

        attribute.DomainEvents.Should().BeEmpty("SaveChangesAsync must clear raised events once persisted");

        // Force a fresh read from PostgreSQL (not the tracked in-memory instance) to prove the
        // owned attribute_values table round-trips through EF's backing-field mapping for real.
        _dbContext.ChangeTracker.Clear();
        var reloaded = await _dbContext.Attributes.FirstAsync(a => a.Id == attribute.Id);
        reloaded.Values.Should().HaveCount(2);
        reloaded.Values.Select(v => v.Value).Should().BeEquivalentTo("Red", "Blue");

        var outboxRows = await _dbContext.AttributeOutboxEvents.Where(e => e.AttributeId == attribute.Id).ToListAsync();
        outboxRows.Should().ContainSingle();
        var row = outboxRows[0];
        row.EventType.Should().Be("AttributeUpdated");
        row.CreatedBy.Should().Be("admin-principal");
        row.PublishedAt.Should().BeNull();
        // jsonb round-trips through Postgres's own canonical text representation (a space after
        // every ':'/',' ), unlike the compact string this row was originally written with - assert
        // content, not exact whitespace.
        row.Payload.Should().Contain("\"operation\"").And.Contain("\"created\"").And.Contain("Red").And.Contain("Blue");
    }

    [Fact]
    public async Task SaveChangesAsync_OnAttributeUpdate_ReplacesValuesAndWritesSecondOutboxRow()
    {
        var attribute = ProductAttribute.Create("Color", null, AttributeDataType.Select, [("Red", 0)], "admin-principal", DateTimeOffset.UtcNow).Value;
        _dbContext.Attributes.Add(attribute);
        await _dbContext.SaveChangesAsync();

        attribute.Update("Primary Color", [("Red", 0), ("Green", 1)], "admin-principal-2", DateTimeOffset.UtcNow);
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();
        var reloaded = await _dbContext.Attributes.FirstAsync(a => a.Id == attribute.Id);
        reloaded.Name.Should().Be("Primary Color");
        reloaded.Values.Should().HaveCount(2);

        var outboxRows = await _dbContext.AttributeOutboxEvents.Where(e => e.AttributeId == attribute.Id).OrderBy(e => e.OccurredAt).ToListAsync();
        outboxRows.Should().HaveCount(2);
        outboxRows[1].Payload.Should().Contain("\"operation\"").And.Contain("\"updated\"");
    }
}
