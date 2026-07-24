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

    /// <summary>
    /// Opens the ambient transaction MoveCategory's SELECT ... FOR UPDATE locks (design-decisions.md,
    /// "Concurrency Control for Hierarchy Mutations") must run inside - a lock taken outside an
    /// explicit transaction is released the instant its own statement completes.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken);

    Task CommitTransactionAsync(CancellationToken cancellationToken);

    Task RollbackTransactionAsync(CancellationToken cancellationToken);
}
