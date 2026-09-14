namespace DataInjectorService.Services;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// ASP.NET Core health check that probes the WeakApp <c>/health</c> endpoint.
/// Reported as <see cref="HealthStatus.Degraded"/> (not unhealthy) when WeakApp is unreachable,
/// because the polling service recovers automatically once WeakApp comes back.
/// </summary>
public sealed class WeakAppHealthCheck(IWeakAppService weakAppService) : IHealthCheck
{
    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        var healthy = await weakAppService.IsHealthyAsync(cancellationToken);

        return healthy
            ? HealthCheckResult.Healthy("WeakApp is reachable")
            : HealthCheckResult.Degraded("WeakApp is not reachable");
    }
}
