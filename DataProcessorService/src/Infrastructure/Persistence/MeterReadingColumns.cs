namespace DataProcessorService.Infrastructure.Persistence;

/// <summary>
/// Physical table and column names for the meter readings table.
/// <para>
/// Ingestion writes through a hand-written <c>INSERT ... ON CONFLICT DO NOTHING</c> statement,
/// because EF Core exposes no insert-from-select and no upsert API. That statement is built from
/// these constants, and a contract test asserts they still match the EF model --- otherwise a
/// column rename would update the model and the migration happily and break ingestion at runtime.
/// </para>
/// </summary>
public static class MeterReadingColumns
{
    /// <summary>Physical table name.</summary>
    public const string Table = "meter_readings";

    /// <summary>Surrogate primary key column.</summary>
    public const string Id = "id";

    /// <summary>Foreign key to the owning sensor.</summary>
    public const string SensorId = "sensor_id";

    /// <summary>Collection timestamp, stored as <c>timestamptz</c>.</summary>
    public const string CollectedAt = "collected_at";

    /// <summary>Table-per-hierarchy discriminator column.</summary>
    public const string SensorType = "sensor_type";

    /// <summary>CO2 concentration, populated for air quality readings only.</summary>
    public const string Co2 = "co2";

    /// <summary>PM2.5 concentration, populated for air quality readings only.</summary>
    public const string Pm25 = "pm25";

    /// <summary>Relative humidity, populated for air quality readings only.</summary>
    public const string Humidity = "humidity";

    /// <summary>Motion flag, populated for motion readings only.</summary>
    public const string MotionDetected = "motion_detected";

    /// <summary>Energy consumption, populated for energy readings only.</summary>
    public const string EnergyKwh = "energy_kwh";

    /// <summary>Unique index backing idempotent ingestion and the ON CONFLICT target.</summary>
    public const string UniqueIndex = "ux_meter_readings_sensor_collected";

    /// <summary>
    /// Every column written by the ingestion statement, in the order the statement lists them.
    /// </summary>
    public static readonly string[] InsertColumns =
    [
        SensorId,
        CollectedAt,
        SensorType,
        Co2,
        Pm25,
        Humidity,
        MotionDetected,
        EnergyKwh,
    ];
}
