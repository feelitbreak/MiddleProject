namespace DataProcessorService.IntegrationTests.Persistence;

using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Contracts;
using DataProcessorService.Domain.Common;
using DataProcessorService.Domain.Entities;
using DataProcessorService.Domain.Enums;
using DataProcessorService.Infrastructure.Persistence.Queries;

/// <summary>
/// Covers bucketed aggregation against a real database.
/// <para>
/// The test that matters most here runs the session in a non-UTC time zone.
/// <c>date_trunc</c> truncates in the session time zone, so a server on local time would silently
/// shift every period boundary; nothing but a non-UTC session proves the explicit UTC argument is
/// doing its job.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class ReadingAggregationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Midnight = UtcInstant.Normalize(
        new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)
    );

    [Fact]
    public async Task AggregateAsync_HourlyPeriods_AlignToTheHour()
    {
        await SeedEnergyAsync([(0, 10), (20, 20), (40, 30), (70, 100)]);

        var periods = await AggregateAsync(
            ReadingMetric.EnergyKwh,
            AggregationInterval.Hour,
            Midnight,
            Midnight.AddHours(3)
        );

        Assert.Equal(2, periods.Count);
        Assert.Equal(Midnight, periods[0].PeriodStart);
        Assert.Equal(Midnight.AddHours(1), periods[1].PeriodStart);
    }

    [Fact]
    public async Task AggregateAsync_Period_ReportsCountAverageMinimumMaximum()
    {
        await SeedEnergyAsync([(0, 10), (20, 20), (40, 30)]);

        var period = Assert.Single(
            await AggregateAsync(
                ReadingMetric.EnergyKwh,
                AggregationInterval.Hour,
                Midnight,
                Midnight.AddHours(1)
            )
        );

        Assert.Equal(3, period.Count);
        Assert.Equal(20, period.Average);
        Assert.Equal(10, period.Minimum);
        Assert.Equal(30, period.Maximum);
        Assert.Equal("Kitchen", period.Location);
    }

    [Fact]
    public async Task AggregateAsync_NonUtcSession_StillAlignsPeriodsToUtc()
    {
        // 02:00Z and 23:00Z are the same UTC day but different days in New York, which is four
        // hours behind in May. A session-local truncation would split these into two periods.
        await SeedEnergyAsync([(120, 10), (1380, 20)]);

        var periods = await AggregateAsync(
            ReadingMetric.EnergyKwh,
            AggregationInterval.Day,
            Midnight,
            Midnight.AddDays(1),
            timeZone: "America/New_York"
        );

        var period = Assert.Single(periods);
        Assert.Equal(Midnight, period.PeriodStart);
        Assert.Equal(2, period.Count);
    }

    [Fact]
    public async Task AggregateAsync_MotionMetric_AveragesToTheDetectionRate()
    {
        await fixture.ResetAsync();
        var sensorId = await SeedSensorAsync("Garage", SensorType.Motion);

        await using (var context = fixture.CreateContext())
        {
            foreach (var (offsetMinutes, detected) in new[]
            {
                (0, true),
                (10, true),
                (20, false),
                (30, false),
            })
            {
                context.MeterReadings.Add(
                    new MotionReading
                    {
                        SensorId = sensorId,
                        CollectedAt = Midnight.AddMinutes(offsetMinutes),
                        MotionDetected = detected,
                    }
                );
            }

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var period = Assert.Single(
            await AggregateAsync(
                ReadingMetric.MotionDetected,
                AggregationInterval.Hour,
                Midnight,
                Midnight.AddHours(1)
            )
        );

        Assert.Equal(0.5, period.Average);
        Assert.Equal(0, period.Minimum);
        Assert.Equal(1, period.Maximum);
    }

    [Fact]
    public async Task AggregateAsync_MetricSelectsTheSensorType()
    {
        // One endpoint serves every reading kind because the metric implies its sensor type, so
        // asking for CO2 must not pick up the energy readings sharing the window.
        await SeedEnergyAsync([(0, 10)]);
        var officeId = await SeedSensorAsync("Office", SensorType.AirQuality);

        await using (var context = fixture.CreateContext())
        {
            context.MeterReadings.Add(
                new AirQualityReading
                {
                    SensorId = officeId,
                    CollectedAt = Midnight,
                    Co2 = 415,
                    Pm25 = 7,
                    Humidity = 44,
                }
            );
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var period = Assert.Single(
            await AggregateAsync(
                ReadingMetric.Co2,
                AggregationInterval.Hour,
                Midnight,
                Midnight.AddHours(1)
            )
        );

        Assert.Equal("Office", period.Location);
        Assert.Equal(415, period.Average);
    }

    [Fact]
    public async Task AggregateAsync_GroupsByLocationAsWellAsPeriod()
    {
        await fixture.ResetAsync();
        var kitchen = await SeedSensorAsync("Kitchen", SensorType.Energy);
        var hall = await SeedSensorAsync("Hall", SensorType.Energy);

        await using (var context = fixture.CreateContext())
        {
            context.MeterReadings.AddRange(
                new EnergyReading { SensorId = kitchen, CollectedAt = Midnight, EnergyKwh = 10 },
                new EnergyReading { SensorId = hall, CollectedAt = Midnight, EnergyKwh = 20 }
            );
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var periods = await AggregateAsync(
            ReadingMetric.EnergyKwh,
            AggregationInterval.Hour,
            Midnight,
            Midnight.AddHours(1)
        );

        Assert.Equal(2, periods.Count);
        Assert.Equal(["Hall", "Kitchen"], periods.Select(p => p.Location).Order());
    }

    [Fact]
    public async Task AggregateAsync_LocationFilter_NarrowsToOneLocation()
    {
        await fixture.ResetAsync();
        var kitchen = await SeedSensorAsync("Kitchen", SensorType.Energy);
        var hall = await SeedSensorAsync("Hall", SensorType.Energy);

        await using (var context = fixture.CreateContext())
        {
            context.MeterReadings.AddRange(
                new EnergyReading { SensorId = kitchen, CollectedAt = Midnight, EnergyKwh = 10 },
                new EnergyReading { SensorId = hall, CollectedAt = Midnight, EnergyKwh = 20 }
            );
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var period = Assert.Single(
            await AggregateAsync(
                ReadingMetric.EnergyKwh,
                AggregationInterval.Hour,
                Midnight,
                Midnight.AddHours(1),
                location: "Kitchen"
            )
        );

        Assert.Equal("Kitchen", period.Location);
    }

    [Fact]
    public async Task AggregateAsync_ReadingsOutsideTheRange_AreExcluded()
    {
        await SeedEnergyAsync([(-60, 999), (0, 10), (120, 999)]);

        var period = Assert.Single(
            await AggregateAsync(
                ReadingMetric.EnergyKwh,
                AggregationInterval.Hour,
                Midnight,
                Midnight.AddHours(1)
            )
        );

        Assert.Equal(1, period.Count);
        Assert.Equal(10, period.Average);
    }

    private async Task<IReadOnlyList<AggregatePeriodDto>> AggregateAsync(
        ReadingMetric metric,
        AggregationInterval interval,
        DateTimeOffset from,
        DateTimeOffset to,
        string? location = null,
        string timeZone = "UTC"
    )
    {
        await using var context = fixture.CreateContext(timeZone);
        var queries = new ReadingQueries(context);

        var periods = await queries.AggregateAsync(
            new AggregateFilter(metric, interval, from, to, location),
            TestContext.Current.CancellationToken
        );

        return periods;
    }

    private async Task<int> SeedSensorAsync(string name, SensorType type)
    {
        await using var context = fixture.CreateContext();
        var sensor = new Sensor { Name = name, Type = type };
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return sensor.Id;
    }

    private async Task SeedEnergyAsync((int OffsetMinutes, double Kwh)[] readings)
    {
        await fixture.ResetAsync();
        var sensorId = await SeedSensorAsync("Kitchen", SensorType.Energy);

        await using var context = fixture.CreateContext();

        foreach (var (offsetMinutes, kwh) in readings)
        {
            context.MeterReadings.Add(
                new EnergyReading
                {
                    SensorId = sensorId,
                    CollectedAt = Midnight.AddMinutes(offsetMinutes),
                    EnergyKwh = kwh,
                }
            );
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
