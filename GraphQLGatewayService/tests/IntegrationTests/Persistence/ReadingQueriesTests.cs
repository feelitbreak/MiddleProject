namespace GraphQLGatewayService.IntegrationTests.Persistence;

using GraphQLGatewayService.Domain.Common;
using GraphQLGatewayService.Domain.Contracts;
using GraphQLGatewayService.Domain.Enums;
using GraphQLGatewayService.Infrastructure.Persistence.Queries;
using GraphQLGatewayService.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Covers filtering, the table-per-hierarchy projection, and latest-per-sensor against a real
/// database. The projection matters most: it reads subtype columns through casts, which only
/// PostgreSQL can confirm translate correctly.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ReadingQueriesTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Start = UtcInstant.Normalize(
        new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)
    );

    [Fact]
    public async Task ProjectToReading_AirQualityRow_PopulatesOnlyItsOwnValues()
    {
        await this.SeedOneOfEachAsync();
        await using var context = fixture.CreateContext();

        var reading = await context
            .FilterReadings(new ReadingFilter { SensorType = SensorType.AirQuality })
            .ProjectToReading()
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(700, reading.Co2);
        Assert.Equal(12, reading.Pm25);
        Assert.Equal(55, reading.Humidity);
        Assert.Null(reading.MotionDetected);
        Assert.Null(reading.EnergyKwh);
    }

    [Fact]
    public async Task ProjectToReading_MotionRow_PopulatesOnlyItsOwnValues()
    {
        await this.SeedOneOfEachAsync();
        await using var context = fixture.CreateContext();

        var reading = await context
            .FilterReadings(new ReadingFilter { SensorType = SensorType.Motion })
            .ProjectToReading()
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.True(reading.MotionDetected);
        Assert.Null(reading.Co2);
        Assert.Null(reading.EnergyKwh);
    }

    [Fact]
    public async Task ProjectToReading_EnergyRow_PopulatesOnlyItsOwnValues()
    {
        await this.SeedOneOfEachAsync();
        await using var context = fixture.CreateContext();

        var reading = await context
            .FilterReadings(new ReadingFilter { SensorType = SensorType.Energy })
            .ProjectToReading()
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3.5, reading.EnergyKwh);
        Assert.Null(reading.Co2);
        Assert.Null(reading.MotionDetected);
    }

    [Fact]
    public async Task ProjectToReading_Always_IncludesTheSensorWithoutASecondQuery()
    {
        await this.SeedOneOfEachAsync();
        await using var context = fixture.CreateContext();

        var reading = await context
            .FilterReadings(new ReadingFilter { SensorType = SensorType.Energy })
            .ProjectToReading()
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Kitchen", reading.Sensor.Name);
        Assert.Equal(SensorType.Energy, reading.Sensor.Type);
    }

    [Fact]
    public async Task FilterReadings_NullFilter_ReturnsEverything()
    {
        await this.SeedOneOfEachAsync();
        await using var context = fixture.CreateContext();

        Assert.Equal(3, await context.FilterReadings(null).CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FilterReadings_Location_NarrowsToThatLocation()
    {
        await this.SeedTwoLocationsAsync();
        await using var context = fixture.CreateContext();

        var readings = await context
            .FilterReadings(new ReadingFilter { Location = "Garage" })
            .ProjectToReading()
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(readings);
        Assert.All(readings, reading => Assert.Equal("Garage", reading.Sensor.Name));
    }

    [Fact]
    public async Task FilterReadings_LocationWithSurroundingWhitespace_IsTrimmed()
    {
        await this.SeedTwoLocationsAsync();
        await using var context = fixture.CreateContext();

        var readings = await context
            .FilterReadings(new ReadingFilter { Location = "  Garage  " })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(readings);
    }

    [Fact]
    public async Task FilterReadings_TimeBounds_AreLowerInclusiveAndUpperExclusive()
    {
        await fixture.ResetAsync();
        var sensor = PostgresFixture.Sensor("Kitchen", SensorType.Energy);
        await fixture.SeedAsync(
            Energy(sensor, Start, 1),
            Energy(sensor, Start.AddHours(1), 2),
            Energy(sensor, Start.AddHours(2), 3)
        );

        await using var context = fixture.CreateContext();
        var readings = await context
            .FilterReadings(new ReadingFilter { From = Start, To = Start.AddHours(2) })
            .ProjectToReading()
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, readings.Count);
        Assert.DoesNotContain(readings, reading => reading.CollectedAt == Start.AddHours(2));
    }

    [Fact]
    public async Task FilterReadings_BoundsWithANonUtcOffset_SelectTheSameInstants()
    {
        await fixture.ResetAsync();
        var sensor = PostgresFixture.Sensor("Kitchen", SensorType.Energy);
        await fixture.SeedAsync(
            Energy(sensor, Start, 1),
            Energy(sensor, Start.AddHours(1), 2),
            Energy(sensor, Start.AddHours(2), 3)
        );
        var offset = TimeSpan.FromHours(3);

        await using var context = fixture.CreateContext();
        var count = await context
            .FilterReadings(
                new ReadingFilter
                {
                    From = Start.ToOffset(offset),
                    To = Start.AddHours(2).ToOffset(offset),
                }
            )
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task LatestPerSensor_BoundsWithANonUtcOffset_SelectTheSameInstants()
    {
        await fixture.ResetAsync();
        var kitchen = PostgresFixture.Sensor("Kitchen", SensorType.Energy);
        await fixture.SeedAsync(Energy(kitchen, Start, 1));

        await using var context = fixture.CreateContext();
        var latest = await context
            .LatestPerSensor(new ReadingFilter { From = Start.ToOffset(TimeSpan.FromHours(3)) })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(latest);
    }

    [Fact]
    public async Task LatestPerSensor_Always_ReturnsOneNewestRowPerSensor()
    {
        await fixture.ResetAsync();
        var kitchen = PostgresFixture.Sensor("Kitchen", SensorType.Energy);
        var garage = PostgresFixture.Sensor("Garage", SensorType.Energy);
        await fixture.SeedAsync(
            Energy(kitchen, Start, 1),
            Energy(kitchen, Start.AddHours(2), 2),
            Energy(garage, Start.AddHours(1), 9)
        );

        await using var context = fixture.CreateContext();
        var latest = await context
            .LatestPerSensor(null)
            .ProjectToReading()
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, latest.Count);
        Assert.Equal(2, latest.Single(r => r.Sensor.Name == "Kitchen").EnergyKwh);
        Assert.Equal(9, latest.Single(r => r.Sensor.Name == "Garage").EnergyKwh);
    }

    [Fact]
    public async Task LatestPerSensor_SensorWithNoReadingsInRange_IsOmitted()
    {
        await fixture.ResetAsync();
        var kitchen = PostgresFixture.Sensor("Kitchen", SensorType.Energy);
        await fixture.SeedAsync(Energy(kitchen, Start, 1));

        await using var context = fixture.CreateContext();
        var latest = await context
            .LatestPerSensor(new ReadingFilter { From = Start.AddDays(1) })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(latest);
    }

    [Fact]
    public async Task Locations_DuplicateNamesAcrossSensorTypes_AreReturnedOnce()
    {
        await fixture.ResetAsync();
        await fixture.SeedAsync(
            Energy(PostgresFixture.Sensor("Kitchen", SensorType.Energy), Start, 1),
            Motion(PostgresFixture.Sensor("Kitchen", SensorType.Motion), Start, true),
            Energy(PostgresFixture.Sensor("Garage", SensorType.Energy), Start, 2)
        );

        await using var context = fixture.CreateContext();
        var locations = await context.Locations().ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Garage", "Kitchen"], locations);
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

    private async Task SeedOneOfEachAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedAsync(
            new AirQualityReadingRow
            {
                Sensor = PostgresFixture.Sensor("Kitchen", SensorType.AirQuality),
                CollectedAt = Start,
                Co2 = 700,
                Pm25 = 12,
                Humidity = 55,
            },
            Motion(PostgresFixture.Sensor("Kitchen", SensorType.Motion), Start, detected: true),
            Energy(PostgresFixture.Sensor("Kitchen", SensorType.Energy), Start, 3.5)
        );
    }

    private async Task SeedTwoLocationsAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedAsync(
            Energy(PostgresFixture.Sensor("Kitchen", SensorType.Energy), Start, 1),
            Energy(PostgresFixture.Sensor("Garage", SensorType.Energy), Start, 2)
        );
    }
}
