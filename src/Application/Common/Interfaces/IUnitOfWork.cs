namespace KartCategoryService.Application.Common.Interfaces;

/// <summary>
/// Commits the PostgreSQL transaction for the current request. Infrastructure's implementation
/// also converts any pending domain events raised on tracked aggregates into
/// category_outbox_events rows within this same call (design-decisions.md, "Reliable Event
/// Publication for CategoryUpdated") - Application code never writes outbox rows itself.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
