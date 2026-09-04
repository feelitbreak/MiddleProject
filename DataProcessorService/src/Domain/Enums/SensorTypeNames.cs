namespace DataProcessorService.Domain.Enums;

/// <summary>
/// Canonical string form of <see cref="SensorType"/>.
/// <para>
/// These are the values the upstream API uses on the wire, and they are stored verbatim in the
/// database so that the vocabulary is identical end to end and ad-hoc SQL reads naturally. Both
/// the <c>sensors.sensor_type</c> column and the <c>meter_readings</c> table-per-hierarchy
/// discriminator use them.
/// </para>
/// </summary>
public static class SensorTypeNames
{
    /// <summary>Wire and storage name of <see cref="SensorType.AirQuality"/>.</summary>
    public const string AirQuality = "air_quality";

    /// <summary>Wire and storage name of <see cref="SensorType.Motion"/>.</summary>
    public const string Motion = "motion";

    /// <summary>Wire and storage name of <see cref="SensorType.Energy"/>.</summary>
    public const string Energy = "energy";

    /// <summary>Converts a <see cref="SensorType"/> to its canonical string form.</summary>
    /// <param name="type">The sensor type.</param>
    /// <returns>The canonical string form.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a known sensor type.</exception>
    public static string ToName(SensorType type) =>
        type switch
        {
            SensorType.AirQuality => AirQuality,
            SensorType.Motion => Motion,
            SensorType.Energy => Energy,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown sensor type."),
        };

    /// <summary>
    /// Converts a canonical string form back to a <see cref="SensorType"/>.
    /// </summary>
    /// <param name="name">The canonical string form.</param>
    /// <returns>The matching sensor type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a known sensor type name.</exception>
    public static SensorType FromName(string name) =>
        name switch
        {
            AirQuality => SensorType.AirQuality,
            Motion => SensorType.Motion,
            Energy => SensorType.Energy,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown sensor type."),
        };

    /// <summary>
    /// Attempts to convert a canonical string form back to a <see cref="SensorType"/>, without
    /// throwing. Used on the ingestion path, where an unrecognised type is a poison message to be
    /// dead-lettered rather than an exceptional condition.
    /// </summary>
    /// <param name="name">The candidate name.</param>
    /// <param name="type">The matching sensor type, when recognised.</param>
    /// <returns><see langword="true"/> when <paramref name="name"/> is a known sensor type.</returns>
    public static bool TryFromName(string? name, out SensorType type)
    {
        switch (name)
        {
            case AirQuality:
                type = SensorType.AirQuality;
                return true;
            case Motion:
                type = SensorType.Motion;
                return true;
            case Energy:
                type = SensorType.Energy;
                return true;
            default:
                type = default;
                return false;
        }
    }
}
