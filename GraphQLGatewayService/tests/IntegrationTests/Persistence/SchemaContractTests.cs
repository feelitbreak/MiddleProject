namespace GraphQLGatewayService.IntegrationTests.Persistence;

using GraphQLGatewayService.Domain.Enums;
using GraphQLGatewayService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Pins the physical schema this service reads.
/// <para>
/// DataProcessorService owns these tables and migrates them; the mapping here is a duplicate that
/// nothing forces to stay in step. The expected names below mirror that service's InitialSchema
/// migration, so a rename there fails here instead of at runtime.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class SchemaContractTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Model_MeterReadings_MapsTheExpectedColumns()
    {
        var columns = await this.ColumnsOfAsync(MeterReadingColumns.Table);

        Assert.Equal(
            new[]
            {
                MeterReadingColumns.Co2,
                MeterReadingColumns.CollectedAt,
                MeterReadingColumns.EnergyKwh,
                MeterReadingColumns.Humidity,
                MeterReadingColumns.Id,
                MeterReadingColumns.MotionDetected,
                MeterReadingColumns.Pm25,
                MeterReadingColumns.SensorId,
                MeterReadingColumns.SensorType,
            }.OrderBy(column => column, StringComparer.Ordinal),
            columns
        );
    }

    [Fact]
    public async Task Model_Sensors_MapsTheExpectedColumns() =>
        Assert.Equal(["id", "name", "sensor_type"], await this.ColumnsOfAsync("sensors"));

    [Fact]
    public async Task Model_CollectedAt_IsStoredWithATimeZone()
    {
        await using var context = fixture.CreateContext();

        var type = await context
            .Database.SqlQuery<string>(
                $"""
                SELECT data_type AS "Value" FROM information_schema.columns
                WHERE table_name = 'meter_readings' AND column_name = 'collected_at'
                """
            )
            .SingleAsync(TestContext.Current.CancellationToken);

        // A plain "timestamp without time zone" would silently drop the offset and make every
        // date_trunc bucket depend on the server's clock.
        Assert.Equal("timestamp with time zone", type);
    }

    [Fact]
    public async Task Model_Indexes_CoverTheQueriesThisServiceIssues()
    {
        await using var context = fixture.CreateContext();

        var indexes = await context
            .Database.SqlQuery<string>(
                $"SELECT indexname AS \"Value\" FROM pg_indexes WHERE tablename = 'meter_readings'"
            )
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Contains("ix_meter_readings_collected_id", indexes);
        Assert.Contains(MeterReadingColumns.UniqueIndex, indexes);
    }

    [Fact]
    public async Task Model_Discriminator_UsesTheStoredSpellings()
    {
        await using var context = fixture.CreateContext();

        var discriminator = context
            .Model.FindEntityType(typeof(Infrastructure.Persistence.ReadModels.MeterReadingRow))!
            .FindDiscriminatorProperty();

        Assert.NotNull(discriminator);
        Assert.Equal(MeterReadingColumns.SensorType, discriminator.GetColumnName());

        var values = context
            .Model.GetEntityTypes()
            .Where(type => type.BaseType is not null)
            .Select(type => type.GetDiscriminatorValue())
            .OfType<string>()
            .OrderBy(value => value, StringComparer.Ordinal);

        Assert.Equal(
            [
                SensorTypeNames.ToName(SensorType.AirQuality),
                SensorTypeNames.ToName(SensorType.Energy),
                SensorTypeNames.ToName(SensorType.Motion),
            ],
            values
        );
    }

    private async Task<List<string>> ColumnsOfAsync(string table)
    {
        await using var context = fixture.CreateContext();

        var columns = await context
            .Database.SqlQuery<string>(
                $"""
                SELECT column_name AS "Value" FROM information_schema.columns
                WHERE table_name = {table}
                """
            )
            .ToListAsync(TestContext.Current.CancellationToken);

        return [.. columns.OrderBy(column => column, StringComparer.Ordinal)];
    }
}
