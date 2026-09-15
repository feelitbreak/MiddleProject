namespace GraphQLGatewayService.Infrastructure.Persistence;

/// <summary>
/// Physical table and column names, in one place because they must stay identical to
/// DataProcessorService's mapping. That service owns the schema; a rename there breaks this one.
/// </summary>
public static class MeterReadingColumns
{
    public const string Table = "meter_readings";

    public const string Id = "id";

    public const string SensorId = "sensor_id";

    /// <summary>Collection timestamp, stored as <c>timestamptz</c>.</summary>
    public const string CollectedAt = "collected_at";

    /// <summary>Table-per-hierarchy discriminator column.</summary>
    public const string SensorType = "sensor_type";

    /// <summary>Populated for air quality readings only.</summary>
    public const string Co2 = "co2";

    /// <summary>Populated for air quality readings only.</summary>
    public const string Pm25 = "pm25";

    /// <summary>Populated for air quality readings only.</summary>
    public const string Humidity = "humidity";

    /// <summary>Populated for motion readings only.</summary>
    public const string MotionDetected = "motion_detected";

    /// <summary>Populated for energy readings only.</summary>
    public const string EnergyKwh = "energy_kwh";

    /// <summary>Unique index over a sensor and its collection instant.</summary>
    public const string UniqueIndex = "ux_meter_readings_sensor_collected";
}
