using KartCategoryService.Domain.Categories;
using KartCategoryService.Infrastructure.Messaging;
using KartCategoryService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace KartCategoryService.Infrastructure.Persistence;

public sealed class CategoryDbContext : DbContext
{
    public CategoryDbContext(DbContextOptions<CategoryDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<CategoryOutboxEvent> OutboxEvents => Set<CategoryOutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CategoryConfiguration());
        modelBuilder.ApplyConfiguration(new CategoryOutboxEventConfiguration());
    }

    /// <summary>
    /// Converts every tracked Category's pending domain events into category_outbox_events rows
    /// within this same SaveChanges call - design-decisions.md, "Reliable Event Publication for
    /// CategoryUpdated": the taxonomy write and "the event will eventually publish" commit
    /// atomically, never as a separate, unguarded publish step.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var categoriesWithEvents = ChangeTracker.Entries<Category>()
            .Select(entry => entry.Entity)
            .Where(category => category.DomainEvents.Count > 0)
            .ToList();

        foreach (var category in categoriesWithEvents)
        {
            foreach (var domainEvent in category.DomainEvents.OfType<CategoryUpdatedDomainEvent>())
            {
                OutboxEvents.Add(CategoryOutboxEvent.FromDomainEvent(domainEvent, category.UpdatedBy));
            }
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var category in categoriesWithEvents)
        {
            category.ClearDomainEvents();
        }

        return result;
    }
}
