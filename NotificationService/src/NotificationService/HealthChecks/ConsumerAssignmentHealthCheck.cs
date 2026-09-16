namespace NotificationService.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using NotificationService.Messaging;

/// <summary>
/// Reports the consumer as ready once it owns at least one partition.
/// <para>
/// Degraded rather than unhealthy while unassigned: an ordinary rebalance briefly leaves a healthy
/// consumer with no partitions, and dropping every WebSocket connection over that would be worse
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
