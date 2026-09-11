namespace DataProcessorService.Domain.Enums;

using System.Text.Json;

/// <summary>
/// Converts <see cref="SensorType"/> to and from the spelling used outside the process: the
/// upstream API's wire format, the <c>sensors.sensor_type</c> column, and the
/// <c>meter_readings</c> table-per-hierarchy discriminator all use <c>air_quality</c>,
/// <c>motion</c> and <c>energy</c>.
/// <para>
/// This exists only because that spelling is a <em>storage format</em> and differs from the C#
/// member names. The API surface has no such constraint and uses the member names directly, which
/// ASP.NET Core and <c>System.Text.Json</c> already handle without help.
/// </para>
/// </summary>
public static class SensorTypeNames
{
    /// <summary>Converts a <see cref="SensorType"/> to its stored spelling.</summary>
    /// <param name="type">The sensor type.</param>
    /// <returns>The stored spelling, for example <c>air_quality</c>.</returns>
    public static string ToName(SensorType type) =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(type.ToString());

    /// <summary>Converts a stored spelling back to a <see cref="SensorType"/>.</summary>
    /// <param name="name">The stored spelling.</param>
    /// <returns>The matching sensor type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a known sensor type name.</exception>
    public static SensorType FromName(string name) =>
        TryFromName(name, out var type)
            ? type
            : throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown sensor type.");

    /// <summary>
    /// Attempts to convert a stored spelling back to a <see cref="SensorType"/>, without throwing.
    /// Used on the ingestion path, where an unrecognised type is a poison message to be
    /// dead-lettered rather than an exceptional condition.
    /// </summary>
    /// <param name="name">The candidate spelling.</param>
    /// <param name="type">The matching sensor type, when recognised.</param>
    /// <returns><see langword="true"/> when <paramref name="name"/> is a known sensor type.</returns>
    public static bool TryFromName(string? name, out SensorType type)
    {
        type = default;

        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        // Dropping the separators turns the stored spelling back into the member name, which
        // Enum.TryParse then matches case-insensitively. The other two clauses are what make this
        // strict rather than merely lenient: Enum.TryParse accepts any numeric string, so without
        // IsDefined a payload declaring "type": "1" would read as a valid sensor type; and the
        // round trip rejects every spelling but the canonical one, so a producer emitting
        // "AirQuality" is dead-lettered as the contract violation it is instead of quietly working.
        return Enum.TryParse(name.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true, out type)
            && Enum.IsDefined(type)
            && string.Equals(ToName(type), name, StringComparison.Ordinal);
    }
}
