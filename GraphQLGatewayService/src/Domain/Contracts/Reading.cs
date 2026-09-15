namespace GraphQLGatewayService.Domain.Contracts;

/// <summary>
/// A single reading, flat: values that do not apply to the sensor's type are null. One uniform
/// type keeps clients free of type switches, and under table-per-hierarchy it costs nothing.
/// </summary>
public sealed class Reading
{
    public long Id { get; init; }

    public Sensor Sensor { get; init; } = new();

    /// <summary>The instant the reading was collected, in UTC.</summary>
    public DateTimeOffset CollectedAt { get; init; }

    /// <summary>CO2 concentration in ppm, for air quality readings.</summary>
    public int? Co2 { get; init; }

    /// <summary>PM2.5 concentration in ug/m3, for air quality readings.</summary>
    public int? Pm25 { get; init; }

    /// <summary>Relative humidity percentage, for air quality readings.</summary>
    public int? Humidity { get; init; }

    public bool? MotionDetected { get; init; }

    /// <summary>Energy consumption in kWh, for energy readings.</summary>
    public double? EnergyKwh { get; init; }
}
