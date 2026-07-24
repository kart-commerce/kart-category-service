namespace KartCategoryService.Domain.Common;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
