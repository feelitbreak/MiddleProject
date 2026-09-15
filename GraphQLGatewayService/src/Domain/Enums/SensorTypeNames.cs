namespace GraphQLGatewayService.Domain.Enums;

using System.Text.Json;

/// <summary>
/// Converts <see cref="SensorType"/> to and from its stored spelling: <c>air_quality</c>,
/// <c>motion</c> and <c>energy</c>, used by both <c>sensors.sensor_type</c> and the
/// <c>meter_readings</c> discriminator.
/// </summary>
public static class SensorTypeNames
{
    public static string ToName(SensorType type) =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(type.ToString());

    public static SensorType FromName(string name) =>
        TryFromName(name, out var type)
            ? type
            : throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown sensor type.");

    /// <summary>Converts a stored spelling back to a <see cref="SensorType"/> without throwing.</summary>
    public static bool TryFromName(string? name, out SensorType type)
    {
        type = default;

        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        // IsDefined and the round trip are what make this strict: Enum.TryParse alone would accept
        // a numeric "1", and any casing of the member name.
        return Enum.TryParse(
                name.Replace("_", string.Empty, StringComparison.Ordinal),
                ignoreCase: true,
                out type
            )
            && Enum.IsDefined(type)
            && string.Equals(ToName(type), name, StringComparison.Ordinal);
    }
}
