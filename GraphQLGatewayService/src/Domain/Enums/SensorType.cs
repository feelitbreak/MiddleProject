namespace GraphQLGatewayService.Domain.Enums;

/// <summary>
/// The kind of sensor a reading came from. Stored as the <c>meter_readings</c> discriminator and
/// the <c>sensors.sensor_type</c> column.
/// </summary>
public enum SensorType
{
    /// <summary>Reports CO2, PM2.5 and humidity.</summary>
    AirQuality = 1,

    /// <summary>Reports whether motion was detected.</summary>
    Motion = 2,

    /// <summary>Reports consumption in kWh.</summary>
    Energy = 3,
}
