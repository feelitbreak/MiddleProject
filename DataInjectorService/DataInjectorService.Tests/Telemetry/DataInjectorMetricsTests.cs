namespace DataInjectorService.Tests.Telemetry;

using DataInjectorService.Telemetry;

/// <summary>Tests the <see cref="DataInjectorMetrics"/> instrument definitions.</summary>
public sealed class DataInjectorMetricsTests
{
    [Fact]
    public void Constructor_CreatesAllInstruments()
    {
        using var metrics = new DataInjectorMetrics();

        Assert.NotNull(metrics.KafkaMessagesProduced);
        Assert.NotNull(metrics.KafkaMessagesFailed);
        Assert.NotNull(metrics.MeterReadingsPolled);
        Assert.NotNull(metrics.WeakAppRequests);
        Assert.NotNull(metrics.PollingCycleDuration);
        Assert.NotNull(metrics.KafkaProduceDuration);
    }

    [Fact]
    public void MeterName_MatchesConstant()
    {
        Assert.Equal("DataInjectorService", DataInjectorMetrics.MeterName);
    }
}
