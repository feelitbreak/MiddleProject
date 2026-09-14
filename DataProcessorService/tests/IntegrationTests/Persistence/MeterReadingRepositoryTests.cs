namespace DataProcessorService.IntegrationTests.Persistence;

using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Domain.Common;
using DataProcessorService.Domain.Entities;
using DataProcessorService.Domain.Enums;
using DataProcessorService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Covers the one hand-written SQL statement in the service, against a real PostgreSQL.
/// <para>
/// This is the guarantee the whole consumer design rests on: Kafka delivers at least once, and the
/// window between committing the database transaction and committing the offset means a crash
/// redelivers a batch that is already stored. Nothing but this statement makes that harmless, and
/// nothing but a real database can prove it does.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class MeterReadingRepositoryTests(PostgresFixture fixture)
    : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task InsertIgnoringDuplicates_MixedSensorTypes_WritesEveryRowInOneStatement()
    {
        await fixture.ResetAsync();
        var sensors = await SeedSensorsAsync();
        var collectedAt = Now();

        var inserted = await InsertAsync(
            [
                new(sensors.Energy, SensorType.Energy, collectedAt, ReadingValues.ForEnergy(12.5)),
                new(
                    sensors.AirQuality,
                    SensorType.AirQuality,
                    collectedAt,
                    ReadingValues.ForAirQuality(415, 7, 44)
                ),
                new(sensors.Motion, SensorType.Motion, collectedAt, ReadingValues.ForMotion(true)),
            ]
        );

        Assert.Equal(3, inserted);

        await using var context = fixture.CreateContext();
        var energy = await context.Set<EnergyReading>().SingleAsync(
            TestContext.Current.CancellationToken
        );
        var air = await context.Set<AirQualityReading>().SingleAsync(
            TestContext.Current.CancellationToken
        );
        var motion = await context.Set<MotionReading>().SingleAsync(
            TestContext.Current.CancellationToken
        );

        Assert.Equal(12.5, energy.EnergyKwh);
        Assert.Equal(415, air.Co2);
        Assert.Equal(7, air.Pm25);
        Assert.Equal(44, air.Humidity);
        Assert.True(motion.MotionDetected);
    }

    [Fact]
    public async Task InsertIgnoringDuplicates_SameBatchTwice_InsertsOnce()
    {
        await fixture.ResetAsync();
        var sensors = await SeedSensorsAsync();
        var collectedAt = Now();

        List<MeterReadingRow> batch =
        [
            new(sensors.Energy, SensorType.Energy, collectedAt, ReadingValues.ForEnergy(1.0)),
            new(
                sensors.Energy,
                SensorType.Energy,
                collectedAt.AddSeconds(1),
                ReadingValues.ForEnergy(2.0)
            ),
        ];

        Assert.Equal(2, await InsertAsync(batch));
        Assert.Equal(0, await InsertAsync(batch));

        await using var context = fixture.CreateContext();
        Assert.Equal(
            2,
            await context.MeterReadings.CountAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task InsertIgnoringDuplicates_PartialOverlap_InsertsOnlyTheNewRows()
    {
        await fixture.ResetAsync();
        var sensors = await SeedSensorsAsync();
        var collectedAt = Now();

        await InsertAsync(
            [new(sensors.Energy, SensorType.Energy, collectedAt, ReadingValues.ForEnergy(1.0))]
        );

        var inserted = await InsertAsync(
            [
                new(sensors.Energy, SensorType.Energy, collectedAt, ReadingValues.ForEnergy(1.0)),
                new(
                    sensors.Energy,
                    SensorType.Energy,
                    collectedAt.AddSeconds(1),
                    ReadingValues.ForEnergy(2.0)
                ),
            ]
        );

        // Rows affected excludes what the conflict clause skipped, which is what the
        // duplicates-skipped metric is derived from.
        Assert.Equal(1, inserted);
    }

    [Fact]
    public async Task InsertIgnoringDuplicates_SameInstantDifferentSensors_BothInserted()
    {
        await fixture.ResetAsync();
        var sensors = await SeedSensorsAsync();
        var collectedAt = Now();

        var inserted = await InsertAsync(
            [
                new(sensors.Energy, SensorType.Energy, collectedAt, ReadingValues.ForEnergy(1.0)),
                new(sensors.Motion, SensorType.Motion, collectedAt, ReadingValues.ForMotion(false)),
            ]
        );

        Assert.Equal(2, inserted);
    }

    [Fact]
    public async Task InsertIgnoringDuplicates_SingleTypeBatch_HandlesAllNullValueColumns()
    {
        // A batch of one sensor type leaves every other value column's array entirely null, which
        // is where a mis-specified array parameter type would surface.
        await fixture.ResetAsync();
        var sensors = await SeedSensorsAsync();
        var collectedAt = Now();

        var inserted = await InsertAsync(
            [
                new(sensors.Motion, SensorType.Motion, collectedAt, ReadingValues.ForMotion(true)),
                new(
                    sensors.Motion,
                    SensorType.Motion,
                    collectedAt.AddSeconds(1),
                    ReadingValues.ForMotion(false)
                ),
            ]
        );

        Assert.Equal(2, inserted);

        await using var context = fixture.CreateContext();
        var readings = await context
            .Set<MotionReading>()
            .OrderBy(reading => reading.CollectedAt)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([true, false], readings.Select(reading => reading.MotionDetected));
    }

    [Fact]
    public async Task InsertIgnoringDuplicates_EmptyBatch_DoesNothing()
    {
        await fixture.ResetAsync();

        Assert.Equal(0, await InsertAsync([]));
    }

    [Fact]
    public async Task InsertIgnoringDuplicates_LargeBatch_StaysWithinTheParameterLimit()
    {
        // Values go in as arrays expanded by unnest precisely so that parameter count does not
        // scale with batch size. A VALUES list of this size would exceed PostgreSQL's limit of
        // 65535 parameters.
        await fixture.ResetAsync();
        var sensors = await SeedSensorsAsync();
        var start = Now();

        var rows = Enumerable
            .Range(0, 10_000)
            .Select(offset => new MeterReadingRow(
                sensors.Energy,
                SensorType.Energy,
                start.AddSeconds(offset),
                ReadingValues.ForEnergy(offset)
            ))
            .ToList();

        Assert.Equal(10_000, await InsertAsync(rows));
    }

    [Fact]
    public async Task InsertIgnoringDuplicates_MicrosecondPrecision_RoundTripsExactly()
    {
        await fixture.ResetAsync();
        var sensors = await SeedSensorsAsync();
        var collectedAt = UtcInstant.Normalize(
            new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero).AddTicks(1_234_567)
        );

        await InsertAsync(
            [new(sensors.Energy, SensorType.Energy, collectedAt, ReadingValues.ForEnergy(1.0))]
        );

        await using var context = fixture.CreateContext();
        var stored = await context.MeterReadings.SingleAsync(
            TestContext.Current.CancellationToken
        );

        // Equality here is what keeps the in-memory deduplication key and the unique index in
        // agreement; a value that survives the round trip unchanged cannot disagree with the index.
        Assert.Equal(collectedAt, stored.CollectedAt);
    }

    private static DateTimeOffset Now() => UtcInstant.Normalize(DateTimeOffset.UtcNow);

    private async Task<int> InsertAsync(IReadOnlyList<MeterReadingRow> rows)
    {
        await using var context = fixture.CreateContext();
        var repository = new MeterReadingRepository(context);

        return await repository.InsertIgnoringDuplicatesAsync(
            rows,
            TestContext.Current.CancellationToken
        );
    }

    private async Task<(int Energy, int AirQuality, int Motion)> SeedSensorsAsync()
    {
        await using var context = fixture.CreateContext();

        var energy = new Sensor { Name = "Kitchen", Type = SensorType.Energy };
        var airQuality = new Sensor { Name = "Office", Type = SensorType.AirQuality };
        var motion = new Sensor { Name = "Garage", Type = SensorType.Motion };

        context.Sensors.AddRange(energy, airQuality, motion);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (energy.Id, airQuality.Id, motion.Id);
    }
}
