namespace DataProcessorService.Domain.Enums;

/// <summary>
/// The kind of sensor a reading came from. Persisted as the table-per-hierarchy discriminator
/// and as the wire discriminator on the inbound Kafka message.
/// </summary>
public enum SensorType
{
    /// <summary>Air quality sensor reporting CO2, PM2.5 and humidity.</summary>
    AirQuality = 1,

    /// <summary>Occupancy sensor reporting whether motion was detected.</summary>
    Motion = 2,

    /// <summary>Energy meter reporting consumption in kWh.</summary>
    Energy = 3,
}
