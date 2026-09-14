namespace DataProcessorService.Api.HealthChecks;

using DataProcessorService.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Reports the consumer as ready once it owns at least one partition.
/// <para>
/// Degraded rather than unhealthy while unassigned: an ordinary rebalance briefly leaves a healthy
/// consumer with no partitions, and taking the instance out of rotation for that would be worse
/// than the condition it reports.
/// </para>
/// </summary>
public sealed class ConsumerAssignmentHealthCheck(ConsumerHeartbeat heartbeat) : IHealthCheck
{
    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    ) =>
        Task.FromResult(
            heartbeat.HasAssignment
                ? HealthCheckResult.Healthy("The consumer owns at least one partition.")
                : HealthCheckResult.Degraded(
                    "The consumer owns no partitions yet; it may be starting or rebalancing."
                )
        );
}
