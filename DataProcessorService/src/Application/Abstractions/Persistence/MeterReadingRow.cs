namespace DataProcessorService.Application.Abstractions.Persistence;

using DataProcessorService.Domain.Enums;

/// <summary>
/// The value columns of a reading, shaped for the table-per-hierarchy table: exactly one group is
/// populated and the rest are <see langword="null"/>, matching which columns apply to the sensor
/// type.
/// </summary>
public sealed class ReadingValues
{
    /// <summary>Gets the CO2 concentration in ppm, for air quality readings.</summary>
    public int? Co2 { get; private init; }

    /// <summary>Gets the PM2.5 concentration in ug/m3, for air quality readings.</summary>
    public int? Pm25 { get; private init; }

    /// <summary>Gets the relative humidity percentage, for air quality readings.</summary>
    public int? Humidity { get; private init; }

    public bool? MotionDetected { get; private init; }

    /// <summary>Gets the energy consumption in kWh, for energy readings.</summary>
    public double? EnergyKwh { get; private init; }

    /// <summary>Creates the value set for an air quality reading.</summary>
    public static ReadingValues ForAirQuality(int co2, int pm25, int humidity) =>
        new()
        {
            Co2 = co2,
            Pm25 = pm25,
            Humidity = humidity,
        };

    /// <summary>Creates the value set for a motion reading.</summary>
    public static ReadingValues ForMotion(bool motionDetected) =>
        new() { MotionDetected = motionDetected };

    public static ReadingValues ForEnergy(double energyKwh) => new() { EnergyKwh = energyKwh };
}

/// <summary>
/// One row ready to be written to the meter readings table, with its sensor already resolved to a
/// surrogate key.
/// </summary>
public sealed class MeterReadingRow(
    int sensorId,
    SensorType sensorType,
    DateTimeOffset collectedAtUtc,
    ReadingValues values
)
{
    public int SensorId { get; } = sensorId;

    /// <summary>Gets the sensor type, written as the table-per-hierarchy discriminator.</summary>
    public SensorType SensorType { get; } = sensorType;

    /// <summary>Gets the collection instant, normalised to UTC.</summary>
    public DateTimeOffset CollectedAtUtc { get; } = collectedAtUtc;

    public ReadingValues Values { get; } = values;
}
