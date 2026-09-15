namespace GraphQLGatewayService.IntegrationTests.Persistence;

using GraphQLGatewayService.Domain.Common;
using GraphQLGatewayService.Domain.Contracts;
using GraphQLGatewayService.Domain.Enums;
using GraphQLGatewayService.Infrastructure.Persistence.Queries;
using GraphQLGatewayService.Infrastructure.Persistence.ReadModels;
using Moq;

/// <summary>
/// Covers the <c>date_trunc</c> grouping. Bucket boundaries are the part that cannot be tested
/// without a real PostgreSQL session, since the function truncates in the session time zone.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AggregateQueriesTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Noon = UtcInstant.Normalize(
        new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero)
    );

    [Fact]
    public async Task AggregateAsync_HourlyInterval_AlignsPeriodsToTheHour()
    {
        await fixture.ResetAsync();
        var sensor = PostgresFixture.Sensor("Kitchen", SensorType.Energy);
        await fixture.SeedAsync(
            Energy(sensor, Noon.AddMinutes(5), 1),
            Energy(sensor, Noon.AddMinutes(45), 3)
        );

        var series = await this.AggregateAsync(ReadingMetric.EnergyKwh, AggregationInterval.Hour);

        var point = Assert.Single(Assert.Single(series).Points);
        Assert.Equal(Noon, point.PeriodStart);
        Assert.Equal(2, point.Count);
    }

    [Fact]
    public async Task AggregateAsync_NonUtcSession_StillBucketsInUtc()
    {
        // The whole point of passing "UTC" explicitly to date_trunc. Run the same query through a
        // session in New York: without the explicit zone these readings would land in a bucket
        // starting at local midnight instead of the UTC hour.
        await fixture.ResetAsync();
        var sensor = PostgresFixture.Sensor("Kitchen", SensorType.Energy);
        await fixture.SeedAsync(Energy(sensor, Noon.AddMinutes(5), 1));

        await using var context = fixture.CreateContext("America/New_York");
        var series = await context.AggregateAsync(
            ReadingMetric.EnergyKwh,
            AggregationInterval.Hour,
            Window(Noon.AddDays(-1), Noon.AddDays(1)),
            location: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(Noon, Assert.Single(Assert.Single(series).Points).PeriodStart);
    }

    [Fact]
    public async Task AggregateAsync_MultipleLocations_ReturnsOneSeriesEach()
    {
        await fixture.ResetAsync();
        await fixture.SeedAsync(
            Energy(PostgresFixture.Sensor("Kitchen", SensorType.Energy), Noon, 1),
            Energy(PostgresFixture.Sensor("Garage", SensorType.Energy), Noon, 2)
        );

        var series = await this.AggregateAsync(ReadingMetric.EnergyKwh, AggregationInterval.Hour);

        Assert.Equal(["Garage", "Kitchen"], series.Select(s => s.Location));
    }

    [Fact]
    public async Task AggregateAsync_SeveralPeriods_AreOrderedOldestFirst()
    {
        await fixture.ResetAsync();
        var sensor = PostgresFixture.Sensor("Kitchen", SensorType.Energy);
        await fixture.SeedAsync(
            Energy(sensor, Noon.AddHours(2), 3),
            Energy(sensor, Noon, 1),
            Energy(sensor, Noon.AddHours(1), 2)
        );

        var series = await this.AggregateAsync(ReadingMetric.EnergyKwh, AggregationInterval.Hour);

        var starts = Assert.Single(series).Points.Select(p => p.PeriodStart).ToList();
        Assert.Equal(starts.OrderBy(value => value), starts);
    }

    [Fact]
    public async Task AggregateAsync_OneBucket_ReportsCountAverageMinimumAndMaximum()
    {
        await fixture.ResetAsync();
        var sensor = PostgresFixture.Sensor("Kitchen", SensorType.Energy);
        await fixture.SeedAsync(
            Energy(sensor, Noon.AddMinutes(1), 2),
            Energy(sensor, Noon.AddMinutes(2), 4),
            Energy(sensor, Noon.AddMinutes(3), 6)
        );

        var point = Assert.Single(
            Assert.Single(await this.AggregateAsync(ReadingMetric.EnergyKwh, AggregationInterval.Hour)).Points
        );

        Assert.Equal(3, point.Count);
        Assert.Equal(4, point.Average, precision: 6);
        Assert.Equal(2, point.Minimum, precision: 6);
        Assert.Equal(6, point.Maximum, precision: 6);
    }

    [Fact]
    public async Task AggregateAsync_MotionMetric_AveragesToTheDetectionRate()
    {
        await fixture.ResetAsync();
        var sensor = PostgresFixture.Sensor("Kitchen", SensorType.Motion);
        await fixture.SeedAsync(
            Motion(sensor, Noon.AddMinutes(1), true),
            Motion(sensor, Noon.AddMinutes(2), true),
            Motion(sensor, Noon.AddMinutes(3), false),
            Motion(sensor, Noon.AddMinutes(4), false)
        );

        var point = Assert.Single(
            Assert.Single(await this.AggregateAsync(ReadingMetric.MotionDetected, AggregationInterval.Hour)).Points
        );

        Assert.Equal(0.5, point.Average, precision: 6);
    }

    [Fact]
    public async Task AggregateAsync_Metric_SelectsOnlyItsOwnSensorType()
    {
        await fixture.ResetAsync();
        await fixture.SeedAsync(
            Energy(PostgresFixture.Sensor("Kitchen", SensorType.Energy), Noon, 5),
            Motion(PostgresFixture.Sensor("Kitchen", SensorType.Motion), Noon, true)
        );

        var series = await this.AggregateAsync(ReadingMetric.EnergyKwh, AggregationInterval.Hour);

        Assert.Equal(1, Assert.Single(Assert.Single(series).Points).Count);
    }

    [Fact]
    public async Task AggregateAsync_EveryMetric_CarriesItsUnit()
    {
        await fixture.ResetAsync();
        await fixture.SeedAsync(
            new AirQualityReadingRow
            {
                Sensor = PostgresFixture.Sensor("Kitchen", SensorType.AirQuality),
                CollectedAt = Noon,
                Co2 = 700,
                Pm25 = 12,
                Humidity = 55,
            }
        );

        var series = await this.AggregateAsync(ReadingMetric.Co2, AggregationInterval.Hour);

        Assert.Equal("ppm", Assert.Single(series).Unit);
    }

    [Fact]
    public async Task AggregateAsync_LocationFilter_NarrowsToThatLocation()
    {
        await fixture.ResetAsync();
        await fixture.SeedAsync(
            Energy(PostgresFixture.Sensor("Kitchen", SensorType.Energy), Noon, 1),
            Energy(PostgresFixture.Sensor("Garage", SensorType.Energy), Noon, 2)
        );

        await using var context = fixture.CreateContext();
        var series = await context.AggregateAsync(
            ReadingMetric.EnergyKwh,
            AggregationInterval.Hour,
            Window(Noon.AddDays(-1), Noon.AddDays(1)),
            location: "Garage",
            TestContext.Current.CancellationToken
        );

        Assert.Equal("Garage", Assert.Single(series).Location);
    }

    [Fact]
    public async Task AggregateAsync_ReadingsOutsideTheWindow_AreExcluded()
    {
        await fixture.ResetAsync();
        var sensor = PostgresFixture.Sensor("Kitchen", SensorType.Energy);
        await fixture.SeedAsync(Energy(sensor, Noon, 1), Energy(sensor, Noon.AddDays(5), 2));

        await using var context = fixture.CreateContext();
        var series = await context.AggregateAsync(
            ReadingMetric.EnergyKwh,
            AggregationInterval.Hour,
            Window(Noon.AddHours(-1), Noon.AddHours(1)),
            location: null,
            TestContext.Current.CancellationToken
        );

        Assert.Single(Assert.Single(series).Points);
    }

    [Fact]
    public async Task AggregateAsync_NoMatchingReadings_ReturnsNoSeries()
    {
        await fixture.ResetAsync();

        Assert.Empty(await this.AggregateAsync(ReadingMetric.EnergyKwh, AggregationInterval.Hour));
    }

    private static AggregateWindow Window(DateTimeOffset from, DateTimeOffset to)
    {
        var provider = new Mock<TimeProvider>();
        provider.Setup(p => p.GetUtcNow()).Returns(to);
        return AggregateWindow.Resolve(AggregationInterval.Hour, from, to, provider.Object).Value;
    }

    private static EnergyReadingRow Energy(SensorRow sensor, DateTimeOffset at, double kwh) =>
        new()
        {
            Sensor = sensor,
            CollectedAt = UtcInstant.Normalize(at),
            EnergyKwh = kwh,
        };

    private static MotionReadingRow Motion(SensorRow sensor, DateTimeOffset at, bool detected) =>
        new()
        {
            Sensor = sensor,
            CollectedAt = UtcInstant.Normalize(at),
            MotionDetected = detected,
        };

    private async Task<IReadOnlyList<AggregateSeries>> AggregateAsync(
        ReadingMetric metric,
        AggregationInterval interval
    )
    {
        await using var context = fixture.CreateContext();
        return await context.AggregateAsync(
            metric,
            interval,
            Window(Noon.AddDays(-1), Noon.AddDays(1)),
            location: null,
            TestContext.Current.CancellationToken
        );
    }
}
