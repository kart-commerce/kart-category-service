namespace KartCategoryService.Infrastructure.Messaging;

/// <summary>
/// A uniform view over a pending-publish row, regardless of which aggregate's outbox table it
/// came from (category_outbox_events, attribute_outbox_events). Lets OutboxRelayHostedService pull
/// from every registered outbox table through one relay/retry/reconnect loop instead of one
/// BackgroundService per aggregate - both tables live in the same CategoryDbContext/transaction
/// boundary, so there is no cross-store consistency concern in merging them at read time.
/// </summary>
public sealed record OutboxEventRecord(
    Guid OutboxId,
    string EventType,
    string Payload,
    DateTimeOffset OccurredAt,
    DateTimeOffset? PublishedAt,
    string? TraceParent,
    Action<DateTimeOffset> MarkPublished);
