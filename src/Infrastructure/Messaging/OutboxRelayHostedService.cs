using Kart.Shared.Messaging;
using Kart.Shared.Observability;
using KartCategoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;

namespace KartCategoryService.Infrastructure.Messaging;

/// <summary>
/// Relays both category_outbox_events and attribute_outbox_events rows to category.exchange
/// (design-decisions.md, "Reliable Event Publication for CategoryUpdated" - now generalized to
/// cover AttributeUpdated too, since Attribute is a second aggregate in this same service/database,
/// not a separate one). One relay/retry/reconnect loop for both tables via the uniform
/// OutboxEventRecord view, merge-sorted by OccurredAt across the two sources so a Category write and
/// an Attribute write interleave in true chronological order rather than one table draining first.
/// Re-declares the manifest's topology idempotently on every (re)connect. Connects lazily with its
/// own retry loop so a RabbitMQ outage at boot degrades publish latency, never crashes the Api
/// process.
/// </summary>
public sealed class OutboxRelayHostedService : BackgroundService
{
    private const string FlowName = "CategoryAttributeManagementAdmin";

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private const int BatchSize = 50;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionFactory _connectionFactory;
    private readonly MessageBusManifest _manifest;
    private readonly ILogger<OutboxRelayHostedService> _logger;

    public OutboxRelayHostedService(
        IServiceScopeFactory scopeFactory,
        IConnectionFactory connectionFactory,
        MessageBusManifest manifest,
        ILogger<OutboxRelayHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionFactory = connectionFactory;
        _manifest = manifest;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                using var channel = connection.CreateModel();
                RabbitMqTopologyProvisioner.Declare(channel, _manifest);

                await RunRelayLoopAsync(channel, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                using var _ = KartFlowContext.Push(FlowName);
                _logger.LogError(
                    ex,
                    "Stage {Stage}: category/attribute outbox relay lost its RabbitMQ connection; reconnecting in {Delay}.",
                    "RabbitMqPublishRetryScheduled",
                    ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task RunRelayLoopAsync(IModel channel, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RelayPendingBatchAsync(channel, stoppingToken);
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task RelayPendingBatchAsync(IModel channel, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CategoryDbContext>();

        var pendingCategoryEvents = await dbContext.OutboxEvents
            .Where(e => e.PublishedAt == null)
            .OrderBy(e => e.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var pendingAttributeEvents = await dbContext.AttributeOutboxEvents
            .Where(e => e.PublishedAt == null)
            .OrderBy(e => e.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var pending = pendingCategoryEvents.Select(e => (OutboxEventRecord)e)
            .Concat(pendingAttributeEvents.Select(e => (OutboxEventRecord)e))
            .OrderBy(e => e.OccurredAt)
            .Take(BatchSize)
            .ToList();

        if (pending.Count == 0)
        {
            return;
        }

        using var _ = KartFlowContext.Push(FlowName);

        foreach (var outboxEvent in pending)
        {
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = outboxEvent.OutboxId.ToString();
            properties.ContentType = "application/json";

            var exchange = _manifest.ExchangeFor(outboxEvent.EventType);
            var routingKey = _manifest.RoutingKeyFor(outboxEvent.EventType);

            // `using var` (not an explicit `using (...) { }` block) so the publish Activity stays
            // current through the Stage log call below too - a real bug found live-verifying this
            // flow: an explicit block here closed the Activity before the log line executed,
            // leaving OutboxEventPublished permanently untagged with any TraceId.
            using var activity = RabbitMqTraceContext.StartPublishActivityFromStoredTraceParent(exchange, routingKey, outboxEvent.TraceParent, properties);

            channel.BasicPublish(
                exchange: exchange,
                routingKey: routingKey,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(outboxEvent.Payload));

            outboxEvent.MarkPublished(DateTimeOffset.UtcNow);

            _logger.LogInformation(
                "Stage {Stage}: {EventType} outbox event {OutboxId} published to {Exchange}/{RoutingKey}",
                "OutboxEventPublished",
                outboxEvent.EventType,
                outboxEvent.OutboxId,
                exchange,
                routingKey);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
