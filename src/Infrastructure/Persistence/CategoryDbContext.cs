using System.Diagnostics;
using KartCategoryService.Domain.Attributes;
using KartCategoryService.Domain.Categories;
using KartCategoryService.Infrastructure.Messaging;
using KartCategoryService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KartCategoryService.Infrastructure.Persistence;

public sealed class CategoryDbContext : DbContext
{
    private readonly ILogger<CategoryDbContext> _logger;

    public CategoryDbContext(DbContextOptions<CategoryDbContext> options, ILogger<CategoryDbContext> logger) : base(options)
    {
        _logger = logger;
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<CategoryOutboxEvent> OutboxEvents => Set<CategoryOutboxEvent>();

    public DbSet<ProductAttribute> Attributes => Set<ProductAttribute>();

    public DbSet<AttributeOutboxEvent> AttributeOutboxEvents => Set<AttributeOutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CategoryConfiguration());
        modelBuilder.ApplyConfiguration(new CategoryOutboxEventConfiguration());
        modelBuilder.ApplyConfiguration(new AttributeConfiguration());
        modelBuilder.ApplyConfiguration(new AttributeOutboxEventConfiguration());
    }

    /// <summary>
    /// Converts every tracked Category's/Attribute's pending domain events into their respective
    /// outbox rows within this same SaveChanges call - design-decisions.md, "Reliable Event
    /// Publication for CategoryUpdated" (the same choke-point pattern now also covers Attribute):
    /// the taxonomy write and "the event will eventually publish" commit atomically, never as a
    /// separate, unguarded publish step. This is also the single point that captures
    /// Activity.Current?.Id onto the outbox row - not any individual command handler - so the
    /// outbox relay (a later, unrelated async context) can resume the *originating* request's
    /// trace instead of starting a disconnected new one.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var traceParent = Activity.Current?.Id;

        var categoriesWithEvents = ChangeTracker.Entries<Category>()
            .Select(entry => entry.Entity)
            .Where(category => category.DomainEvents.Count > 0)
            .ToList();

        // The persisted/outbox-saved log below only runs after `result` confirms the write
        // actually committed - a real bug this flow's own live verification caught: logging
        // before base.SaveChangesAsync runs would still claim a persisted row for a
        // failed/rolled-back write (a timed-out client retry, a transient connection drop).
        var categoryLogEntries = new List<(Guid Id, string Operation)>();
        foreach (var category in categoriesWithEvents)
        {
            foreach (var domainEvent in category.DomainEvents.OfType<CategoryUpdatedDomainEvent>())
            {
                OutboxEvents.Add(CategoryOutboxEvent.FromDomainEvent(domainEvent, category.UpdatedBy, traceParent));
                categoryLogEntries.Add((category.Id, domainEvent.Operation.ToString()));
            }
        }

        var attributesWithEvents = ChangeTracker.Entries<ProductAttribute>()
            .Select(entry => entry.Entity)
            .Where(attribute => attribute.DomainEvents.Count > 0)
            .ToList();

        var attributeLogEntries = new List<(Guid Id, string Operation)>();
        foreach (var attribute in attributesWithEvents)
        {
            foreach (var domainEvent in attribute.DomainEvents.OfType<AttributeUpdatedDomainEvent>())
            {
                AttributeOutboxEvents.Add(AttributeOutboxEvent.FromDomainEvent(domainEvent, attribute.UpdatedBy, traceParent));
                attributeLogEntries.Add((attribute.Id, domainEvent.Operation.ToString()));
            }
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var (id, operation) in categoryLogEntries)
        {
            _logger.LogInformation(
                "Stage {Stage}: category {CategoryId} persisted to categories table (operation {Operation}), CategoryUpdated outbox event saved",
                "CategoryPersistedToDatabase",
                id,
                operation);
        }

        foreach (var (id, operation) in attributeLogEntries)
        {
            _logger.LogInformation(
                "Stage {Stage}: attribute {AttributeId} persisted to attributes table (operation {Operation}), AttributeUpdated outbox event saved",
                "AttributePersistedToDatabase",
                id,
                operation);
        }

        foreach (var category in categoriesWithEvents)
        {
            category.ClearDomainEvents();
        }

        foreach (var attribute in attributesWithEvents)
        {
            attribute.ClearDomainEvents();
        }

        return result;
    }
}
