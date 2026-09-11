namespace DataProcessorService.Domain.Entities;

/// <summary>
/// A single reading, mapped table-per-hierarchy: one table, one discriminator column.
/// <para>
/// The discriminator is a shadow property because the concrete type already carries that
/// information. Query one kind with <c>Set&lt;EnergyReading&gt;()</c>, which filters on it
/// automatically.
/// </para>
/// </summary>
public abstract class MeterReading
{
    /// <summary>Name of the shadow table-per-hierarchy discriminator column.</summary>
    public const string DiscriminatorColumn = "sensor_type";

    public long Id { get; set; }

    public int SensorId { get; set; }

    public Sensor? Sensor { get; set; }

    /// <summary>
    /// When the injector collected the reading; the upstream API supplies no timestamp of its own.
    /// Always normalised to UTC, since <c>timestamptz</c> rejects a non-zero offset. Together with
    /// <see cref="SensorId"/> this is the unique key that makes ingestion idempotent.
    /// </summary>
    public DateTimeOffset CollectedAt { get; set; }
}

/// <summary>A reading from an air quality sensor.</summary>
public sealed class AirQualityReading : MeterReading
{
    /// <summary>CO2 concentration in ppm.</summary>
    public int Co2 { get; set; }

    /// <summary>Fine particulate matter in ug/m3.</summary>
    public int Pm25 { get; set; }

    /// <summary>Relative humidity percentage.</summary>
    public int Humidity { get; set; }
}

/// <summary>A reading from an occupancy sensor.</summary>
public sealed class MotionReading : MeterReading
{
    public bool MotionDetected { get; set; }
}

/// <summary>A reading from an energy meter.</summary>
public sealed class EnergyReading : MeterReading
{
    /// <summary>Energy consumption in kWh.</summary>
    public double EnergyKwh { get; set; }
}
