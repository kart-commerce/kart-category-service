using KartCategoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KartCategoryService.Api.HealthChecks;

/// <summary>Readiness signal for the k8s Helm chart's <c>/health/ready</c> probe - a database that
/// is reachable but behind on migrations (e.g. `category_outbox_events` never created) must fail
/// readiness too, not just an unreachable one, so a pod never accepts traffic while its background
/// workers (<see cref="KartCategoryService.Infrastructure.Messaging.OutboxRelayHostedService"/>)
/// are looping on errors.</summary>
public sealed class CategoryDbHealthCheck(CategoryDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

            return pending.Length == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"{pending.Length} pending migration(s): {string.Join(", ", pending)}");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Category database is unreachable", exception);
        }
    }
}
