namespace NotificationService.UnitTests.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NotificationService.Configuration;
using NotificationService.HealthChecks;
using NotificationService.Messaging;

/// <summary>
/// Covers the probes that report on the consumer rather than on the process.
/// <para>
/// A wedged or evicted consumer leaves the web host answering requests and accepting WebSocket
/// handshakes while pushing nothing, so "the process is up" is not a useful liveness signal here.
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
        var heartbeat = new ConsumerHeartbeat();
        await Task.Delay(30, TestContext.Current.CancellationToken);

        // The same silence read against two thresholds: generous, then one that makes a 30ms gap
        // stale. Liveness is a ratio of silence to the poll interval, not an absolute.
        var lenient = await Liveness(heartbeat, maxPollIntervalMs: 10_000)
            .CheckHealthAsync(new(), TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Healthy, lenient.Status);

        var strict = await Liveness(heartbeat, maxPollIntervalMs: 10)
            .CheckHealthAsync(new(), TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Unhealthy, strict.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_Always_ReportsSilenceAndAssignment()
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
        // An ordinary rebalance briefly leaves a healthy consumer unassigned; dropping every
        // WebSocket connection over that would be worse than the condition being reported.
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

    [Fact]
    public async Task Beat_Always_AdvancesTheLastIterationInstant()
    {
        var heartbeat = new ConsumerHeartbeat();
        var before = heartbeat.LastIterationUtc;

        await Task.Delay(10, TestContext.Current.CancellationToken);
        heartbeat.Beat();

        Assert.True(heartbeat.LastIterationUtc > before);
    }

    private static ConsumerLivenessHealthCheck Liveness(
        ConsumerHeartbeat heartbeat,
        int maxPollIntervalMs
    ) => new(heartbeat, Options.Create(new KafkaOptions { MaxPollIntervalMs = maxPollIntervalMs }));
}
