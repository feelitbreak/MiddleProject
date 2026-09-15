namespace GraphQLGatewayService.Infrastructure.Persistence.ReadModels;

/// <summary>
/// A row of the <c>meter_readings</c> table, which DataProcessorService owns and migrates. Mapped
/// table-per-hierarchy, so the discriminator is a shadow property: query one kind with
/// <c>Set&lt;EnergyReadingRow&gt;()</c>.
/// </summary>
public abstract class MeterReadingRow
{
    public long Id { get; set; }

    public int SensorId { get; set; }

    public SensorRow? Sensor { get; set; }

    /// <summary>Gets or sets when the injector collected the reading. Always UTC.</summary>
    public DateTimeOffset CollectedAt { get; set; }
}

/// <summary>A reading from an air quality sensor.</summary>
public sealed class AirQualityReadingRow : MeterReadingRow
{
    /// <summary>Gets or sets the CO2 concentration in ppm.</summary>
    public int Co2 { get; set; }

    /// <summary>Gets or sets the fine particulate matter in ug/m3.</summary>
    public int Pm25 { get; set; }

    /// <summary>Gets or sets the relative humidity percentage.</summary>
    public int Humidity { get; set; }
}

/// <summary>A reading from an occupancy sensor.</summary>
public sealed class MotionReadingRow : MeterReadingRow
{
    public bool MotionDetected { get; set; }
}

/// <summary>A reading from an energy meter.</summary>
public sealed class EnergyReadingRow : MeterReadingRow
{
    /// <summary>Gets or sets the energy consumption in kWh.</summary>
    public double EnergyKwh { get; set; }
}
