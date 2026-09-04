namespace DataProcessorService.UnitTests.HealthChecks;

using DataProcessorService.Api.HealthChecks;
using DataProcessorService.Infrastructure.Configuration;
using DataProcessorService.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

/// <summary>
/// Covers the probes that report on the consumer rather than on the process.
/// <para>
/// A wedged or evicted consumer leaves the web host answering requests perfectly well while
/// ingesting nothing, so "the process is up" is not a useful liveness signal here.
/// </para>
/// </summary>
public sealed class ConsumerHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_RecentIteration_IsHealthy()
    {
        var heartbeat = new ConsumerHeartbeat();
        heartbeat.Beat();

        var result = await Liveness(heartbeat, maxPollIntervalMs: 300_000)
            .CheckHealthAsync(new(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_StalledLoop_IsUnhealthy()
    {
        // A one-millisecond poll interval makes the threshold two milliseconds, so a heartbeat
        // recorded at construction is already stale by the time the probe runs.
        var heartbeat = new ConsumerHeartbeat();
        await Task.Delay(30, TestContext.Current.CancellationToken);

        var result = await Liveness(heartbeat, maxPollIntervalMs: 10_000)
            .CheckHealthAsync(new(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);

        var strict = await Liveness(heartbeat, maxPollIntervalMs: 10)
            .CheckHealthAsync(new(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, strict.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ReportsSecondsSinceLastIteration()
    {
        var heartbeat = new ConsumerHeartbeat();
        heartbeat.Beat();

        var result = await Liveness(heartbeat, maxPollIntervalMs: 300_000)
            .CheckHealthAsync(new(), TestContext.Current.CancellationToken);

        Assert.Contains("secondsSinceLastIteration", result.Data.Keys);
        Assert.Contains("hasPartitionAssignment", result.Data.Keys);
    }

    [Fact]
    public async Task CheckHealthAsync_NoPartitionAssignment_IsDegradedNotUnhealthy()
    {
        // An ordinary rebalance briefly leaves a healthy consumer unassigned; taking the instance
        // out of rotation for that would be worse than the condition being reported.
        var heartbeat = new ConsumerHeartbeat();

        var result = await new ConsumerAssignmentHealthCheck(heartbeat).CheckHealthAsync(
            new(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WithPartitionAssignment_IsHealthy()
    {
        var heartbeat = new ConsumerHeartbeat();
        heartbeat.SetAssignment(true);

        var result = await new ConsumerAssignmentHealthCheck(heartbeat).CheckHealthAsync(
            new(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    private static ConsumerLivenessHealthCheck Liveness(
        ConsumerHeartbeat heartbeat,
        int maxPollIntervalMs
    ) =>
        new(
            heartbeat,
            Options.Create(new KafkaOptions { MaxPollIntervalMs = maxPollIntervalMs })
        );
}
