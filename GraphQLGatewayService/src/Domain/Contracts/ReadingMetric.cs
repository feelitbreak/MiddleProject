namespace GraphQLGatewayService.Domain.Contracts;

/// <summary>
/// The numeric series being aggregated. Each value implies its sensor type, which is what lets one
/// aggregation query serve every reading kind.
/// </summary>
public enum ReadingMetric
{
    /// <summary>CO2 concentration in ppm.</summary>
    Co2 = 1,

    /// <summary>PM2.5 concentration in ug/m3.</summary>
    Pm25 = 2,

    /// <summary>Relative humidity percentage.</summary>
    Humidity = 3,

    /// <summary>Motion as 1 or 0, so that a period's average is the fraction with motion.</summary>
    MotionDetected = 4,

    /// <summary>Energy consumption in kWh.</summary>
    EnergyKwh = 5,
}
