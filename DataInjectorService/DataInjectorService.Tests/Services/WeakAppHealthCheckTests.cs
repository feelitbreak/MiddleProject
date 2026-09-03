namespace DataInjectorService.Tests.Services;

using DataInjectorService.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

/// <summary>
/// Tests that <see cref="WeakAppHealthCheck"/> maps WeakApp reachability onto the readiness
/// contract: reachable is healthy, unreachable is degraded rather than unhealthy, because the
/// polling service recovers on its own once WeakApp returns.
/// </summary>
public sealed class WeakAppHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WeakAppReachable_ReturnsHealthy()
    {
        var check = BuildCheck(isHealthy: true);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("WeakApp is reachable", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WeakAppUnreachable_ReturnsDegraded()
    {
        var check = BuildCheck(isHealthy: false);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("WeakApp is not reachable", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_PassesCancellationTokenThrough()
    {
        using var cts = new CancellationTokenSource();
        var serviceMock = new Mock<IWeakAppService>();
        serviceMock.Setup(s => s.IsHealthyAsync(cts.Token)).ReturnsAsync(true);

        var check = new WeakAppHealthCheck(serviceMock.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), cts.Token);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        serviceMock.Verify(s => s.IsHealthyAsync(cts.Token), Times.Once);
    }

    private static WeakAppHealthCheck BuildCheck(bool isHealthy)
    {
        var serviceMock = new Mock<IWeakAppService>();
        serviceMock
            .Setup(s => s.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(isHealthy);

        return new WeakAppHealthCheck(serviceMock.Object);
    }
}
