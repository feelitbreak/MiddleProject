namespace GraphQLGatewayService.Domain.Contracts;

/// <summary>
/// The unit each metric is measured in, so a chart can label its own axis instead of the client
/// keeping a lookup table that drifts from the enum.
/// </summary>
public static class ReadingMetricUnits
{
    /// <summary>Returns the display unit for a metric.</summary>
    public static string ToUnit(ReadingMetric metric) =>
        metric switch
        {
            ReadingMetric.Co2 => "ppm",
            ReadingMetric.Pm25 => "ug/m3",
            ReadingMetric.Humidity => "%",
            // Not a physical unit: motion aggregates as 1 and 0, so an average is a fraction.
            ReadingMetric.MotionDetected => "fraction",
            ReadingMetric.EnergyKwh => "kWh",
            _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown metric."),
        };
}
