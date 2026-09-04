namespace DataProcessorService.Domain.Entities;

/// <summary>
/// A single reading recorded by a <see cref="Sensor"/>. Mapped table-per-hierarchy, so every
/// concrete reading type shares one table and is distinguished by a discriminator column.
/// <para>
/// The discriminator is a shadow property rather than a CLR property: the concrete type already
/// carries that information, and a mapped discriminator would need a settable property that
/// duplicates it. Query a single kind with <c>Set&lt;EnergyReading&gt;()</c> or
/// <c>OfType&lt;EnergyReading&gt;()</c>, both of which filter on the discriminator automatically.
/// </para>
/// <para>
/// <see cref="CollectedAt"/> is stamped by the data injector at poll time rather than by the
/// upstream API, which supplies no timestamp of its own. Together with <see cref="SensorId"/> it
/// forms the unique key that makes ingestion idempotent.
/// </para>
/// </summary>
public abstract class MeterReading
{
    /// <summary>Name of the shadow table-per-hierarchy discriminator column.</summary>
    public const string DiscriminatorColumn = "sensor_type";

    /// <summary>Gets or sets the surrogate primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the foreign key of the sensor that produced this reading.</summary>
    public int SensorId { get; set; }

    /// <summary>Gets or sets the sensor that produced this reading.</summary>
    public Sensor? Sensor { get; set; }

    /// <summary>
    /// Gets or sets the UTC instant the reading was collected by the injector. Always normalised
    /// to UTC before persistence: PostgreSQL <c>timestamptz</c> rejects a non-zero offset and
    /// truncates to microsecond resolution.
    /// </summary>
    public DateTimeOffset CollectedAt { get; set; }
}

/// <summary>A reading from an air quality sensor.</summary>
public sealed class AirQualityReading : MeterReading
{
    /// <summary>Gets or sets the CO2 concentration in ppm.</summary>
    public int Co2 { get; set; }

    /// <summary>Gets or sets the fine particulate matter (PM2.5) concentration in ug/m3.</summary>
    public int Pm25 { get; set; }

    /// <summary>Gets or sets the relative humidity percentage.</summary>
    public int Humidity { get; set; }
}

/// <summary>A reading from an occupancy sensor.</summary>
public sealed class MotionReading : MeterReading
{
    /// <summary>Gets or sets a value indicating whether motion was detected.</summary>
    public bool MotionDetected { get; set; }
}

/// <summary>A reading from an energy meter.</summary>
public sealed class EnergyReading : MeterReading
{
    /// <summary>Gets or sets the energy consumption in kWh.</summary>
    public double EnergyKwh { get; set; }
}
