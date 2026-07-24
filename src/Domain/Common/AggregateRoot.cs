namespace KartCategoryService.Domain.Common;

/// <summary>
/// Base for an aggregate root that collects in-process domain events raised during a single
/// unit of work. Infrastructure translates these into Outbox rows within the same SaveChanges
/// transaction (design-decisions.md, "Reliable Event Publication for CategoryUpdated") - never
/// dispatched via an in-memory bus directly.
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; protected init; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
