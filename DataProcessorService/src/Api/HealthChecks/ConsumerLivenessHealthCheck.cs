namespace DataProcessorService.Api.HealthChecks;

using DataProcessorService.Infrastructure.Configuration;
using DataProcessorService.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Globalization;

/// <summary>
/// Reports the consumer loop as live only if it has completed an iteration recently.
/// <para>
/// A plain "the process is up" probe cannot detect the failure that actually matters here: a
/// consumer loop that is wedged, or one the broker has evicted from its group, leaves the web host
/// answering requests normally while nothing is being ingested. The threshold is twice the poll
/// interval, which is the longest the loop can legitimately go quiet before the broker itself
/// would consider it dead.
/// </para>
/// </summary>
/// <param name="heartbeat">The consumer's liveness signal.</param>
/// <param name="options">Kafka configuration options.</param>
public sealed class ConsumerLivenessHealthCheck(
    ConsumerHeartbeat heartbeat,
    IOptions<KafkaOptions> options
) : IHealthCheck
{
    private readonly KafkaOptions options = options.Value;

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        var silence = DateTimeOffset.UtcNow - heartbeat.LastIterationUtc;
        var threshold = TimeSpan.FromMilliseconds(2.0 * this.options.MaxPollIntervalMs);

        var data = new Dictionary<string, object>
        {
            ["secondsSinceLastIteration"] = silence.TotalSeconds.ToString(
                "F1",
                CultureInfo.InvariantCulture
            ),
            ["hasPartitionAssignment"] = heartbeat.HasAssignment,
        };

        return Task.FromResult(
            silence <= threshold
                ? HealthCheckResult.Healthy("The consumer loop is running.", data)
                : HealthCheckResult.Unhealthy(
                    $"The consumer loop has not completed an iteration for {silence.TotalSeconds:F0}s.",
                    data: data
                )
        );
    }
}
