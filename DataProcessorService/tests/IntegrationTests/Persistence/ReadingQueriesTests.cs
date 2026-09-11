namespace DataProcessorService.IntegrationTests.Persistence;

using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Domain.Common;
using DataProcessorService.Domain.Entities;
using DataProcessorService.Domain.Enums;
using DataProcessorService.Infrastructure.Persistence.Queries;

/// <summary>
/// Covers the read side against a real database: filtering, ordering, paging, and the
/// table-per-hierarchy projection.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ReadingQueriesTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Start = UtcInstant.Normalize(
        new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)
    );

    [Fact]
    public async Task ListAsync_Always_ReturnsNewestFirst()
    {
        await SeedAsync();

        var readings = await Query().ListAsync(
            NoFilter,
            skip: 0,
            take: 10,
            TestContext.Current.CancellationToken
        );

        var timestamps = readings.Select(reading => reading.CollectedAt).ToList();
        Assert.Equal(timestamps.OrderByDescending(value => value), timestamps);
    }

    [Fact]
    public async Task ListAsync_ConsecutivePages_DoNotOverlap()
    {
        await SeedAsync();

        var first = await Query().ListAsync(NoFilter, 0, 4, TestContext.Current.CancellationToken);
        var second = await Query().ListAsync(NoFilter, 4, 4, TestContext.Current.CancellationToken);

        Assert.Equal(4, first.Count);
        Assert.Equal(4, second.Count);
        Assert.Empty(first.Select(r => r.Id).Intersect(second.Select(r => r.Id)));
    }

    [Fact]
    public async Task ListAsync_ReadingsSharingAnInstant_PageDeterministically()
    {
        // A whole poll shares one collection instant, so without the id tiebreak the boundary
        // between pages could fall anywhere inside that group and repeat or skip rows.
        await fixture.ResetAsync();
        var sensors = await SeedSensorsAsync();

        await using (var context = fixture.CreateContext())
        {
            foreach (var sensorId in new[] { sensors.Energy, sensors.Energy, sensors.Energy })
            {
                _ = sensorId;
            }

            context.MeterReadings.AddRange(
                Enumerable
                    .Range(0, 6)
                    .Select(index => new EnergyReading
                    {
                        SensorId = sensors.Energy,
                        // Same instant for all six, differing only by microsecond so the unique
                        // index still permits them.
                        CollectedAt = Start.AddTicks(index * 10),
                        EnergyKwh = index,
                    })
            );
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var firstRun = await Query().ListAsync(NoFilter, 0, 3, TestContext.Current.CancellationToken);
        var secondRun = await Query().ListAsync(NoFilter, 0, 3, TestContext.Current.CancellationToken);

        Assert.Equal(firstRun.Select(r => r.Id), secondRun.Select(r => r.Id));
    }

    [Fact]
    public async Task ListAsync_LocationFilter_ReturnsOnlyThatLocation()
    {
        await SeedAsync();

        var readings = await Query().ListAsync(
            new ReadingFilter("Kitchen", null, null, null),
            0,
            50,
            TestContext.Current.CancellationToken
        );

        Assert.NotEmpty(readings);
        Assert.All(readings, reading => Assert.Equal("Kitchen", reading.SensorName));
    }

    [Fact]
    public async Task ListAsync_SensorTypeFilter_ReturnsOnlyThatType()
    {
        await SeedAsync();

        var readings = await Query().ListAsync(
            new ReadingFilter(null, SensorType.Motion, null, null),
            0,
            50,
            TestContext.Current.CancellationToken
        );

        Assert.NotEmpty(readings);
        Assert.All(readings, reading => Assert.Equal(SensorType.Motion, reading.SensorType));
        Assert.All(readings, reading => Assert.NotNull(reading.MotionDetected));
    }

    [Fact]
    public async Task ListAsync_TimeRange_ExcludesTheUpperBound()
    {
        // From is inclusive and To exclusive, so adjacent ranges tile without double-counting.
        await SeedAsync();
        var boundary = Start.AddHours(1);

        var below = await Query().ListAsync(
            new ReadingFilter(null, null, null, boundary),
            0,
            50,
            TestContext.Current.CancellationToken
        );
        var atOrAbove = await Query().ListAsync(
            new ReadingFilter(null, null, boundary, null),
            0,
            50,
            TestContext.Current.CancellationToken
        );

        Assert.All(below, reading => Assert.True(reading.CollectedAt < boundary));
        Assert.All(atOrAbove, reading => Assert.True(reading.CollectedAt >= boundary));
        Assert.Empty(below.Select(r => r.Id).Intersect(atOrAbove.Select(r => r.Id)));
    }

    [Fact]
    public async Task ListAsync_Projection_PopulatesOnlyTheColumnsThatApply()
    {
        // EF cannot project into a hierarchy, so the subtype values are read through casts. This
        // pins that those casts land on the right columns and leave the rest null.
        await SeedAsync();

        var readings = await Query().ListAsync(
            NoFilter,
            0,
            50,
            TestContext.Current.CancellationToken
        );

        var air = readings.First(reading => reading.SensorType == SensorType.AirQuality);
        Assert.Equal(400, air.Co2);
        Assert.Equal(5, air.Pm25);
        Assert.Equal(40, air.Humidity);
        Assert.Null(air.EnergyKwh);
        Assert.Null(air.MotionDetected);

        var energy = readings.First(reading => reading.SensorType == SensorType.Energy);
        Assert.NotNull(energy.EnergyKwh);
        Assert.Null(energy.Co2);

        var motion = readings.First(reading => reading.SensorType == SensorType.Motion);
        Assert.NotNull(motion.MotionDetected);
        Assert.Null(motion.EnergyKwh);
    }

    [Fact]
    public async Task CountAsync_MatchesTheFilterNotThePage()
    {
        await SeedAsync();

        var total = await Query().CountAsync(NoFilter, TestContext.Current.CancellationToken);
        var page = await Query().ListAsync(NoFilter, 0, 2, TestContext.Current.CancellationToken);

        Assert.Equal(9, total);
        Assert.Equal(2, page.Count);
    }

    [Fact]
    public async Task ListLatestPerSensorAsync_ReturnsOneRowPerSensor()
    {
        await SeedAsync();

        var latest = await Query().ListLatestPerSensorAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, latest.Count);
        Assert.Equal(
            latest.Select(reading => (reading.SensorName, reading.SensorType)).Distinct().Count(),
            latest.Count
        );
    }

    [Fact]
    public async Task ListLatestPerSensorAsync_ReturnsTheNewestReading()
    {
        await SeedAsync();

        var latest = await Query().ListLatestPerSensorAsync(TestContext.Current.CancellationToken);
        var energy = latest.Single(reading => reading.SensorType == SensorType.Energy);

        Assert.Equal(Start.AddHours(2), energy.CollectedAt);
    }

    [Fact]
    public async Task ListLatestPerSensorAsync_SensorWithNoReadings_IsOmitted()
    {
        await SeedAsync();

        await using (var context = fixture.CreateContext())
        {
            context.Sensors.Add(new Sensor { Name = "Attic", Type = SensorType.Motion });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var latest = await Query().ListLatestPerSensorAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(latest, reading => reading.SensorName == "Attic");
    }

    private static ReadingFilter NoFilter => new(null, null, null, null);

    private ReadingQueries Query() => new(fixture.CreateContext());

    private async Task<(int Energy, int AirQuality, int Motion)> SeedSensorsAsync()
    {
        await using var context = fixture.CreateContext();

        var energy = new Sensor { Name = "Kitchen", Type = SensorType.Energy };
        var air = new Sensor { Name = "Office", Type = SensorType.AirQuality };
        var motion = new Sensor { Name = "Garage", Type = SensorType.Motion };

        context.Sensors.AddRange(energy, air, motion);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (energy.Id, air.Id, motion.Id);
    }

    /// <summary>Three sensors with three readings each, an hour apart.</summary>
    private async Task SeedAsync()
    {
        await fixture.ResetAsync();
        var sensors = await SeedSensorsAsync();

        await using var context = fixture.CreateContext();

        for (var hour = 0; hour < 3; hour++)
        {
            var at = Start.AddHours(hour);
            context.MeterReadings.AddRange(
                new EnergyReading
                {
                    SensorId = sensors.Energy,
                    CollectedAt = at,
                    EnergyKwh = 10 + hour,
                },
                new AirQualityReading
                {
                    SensorId = sensors.AirQuality,
                    CollectedAt = at,
                    Co2 = 400,
                    Pm25 = 5,
                    Humidity = 40,
                },
                new MotionReading
                {
                    SensorId = sensors.Motion,
                    CollectedAt = at,
                    MotionDetected = hour % 2 == 0,
                }
            );
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
