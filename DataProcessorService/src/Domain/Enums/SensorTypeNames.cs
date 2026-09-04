namespace DataProcessorService.Domain.Enums;

using System.Collections.Frozen;
using System.Text.Json;

/// <summary>
/// Canonical string form of <see cref="SensorType"/>.
/// <para>
/// These are the values the upstream API uses on the wire, and they are stored verbatim in the
/// database so that the vocabulary is identical end to end and ad-hoc SQL reads naturally. Both
/// the <c>sensors.sensor_type</c> column and the <c>meter_readings</c> table-per-hierarchy
/// discriminator use them.
/// </para>
/// <para>
/// Both directions are derived from the enum itself rather than hand-maintained, so adding a
/// sensor type means adding one enum member and nothing else.
/// <see cref="JsonNamingPolicy.SnakeCaseLower"/> is used purely as a naming utility here --- it
/// turns <c>AirQuality</c> into <c>air_quality</c> --- and the exact strings it produces are
/// pinned by unit tests, since they are a storage format and cannot change silently.
/// </para>
/// </summary>
public static class SensorTypeNames
{
    private static readonly FrozenDictionary<SensorType, string> NamesByType = Enum.GetValues<
        SensorType
    >()
        .ToFrozenDictionary(
            type => type,
            type => JsonNamingPolicy.SnakeCaseLower.ConvertName(type.ToString())
        );

    private static readonly FrozenDictionary<string, SensorType> TypesByName =
        NamesByType.ToFrozenDictionary(
            pair => pair.Value,
            pair => pair.Key,
            StringComparer.Ordinal
        );

    /// <summary>Converts a <see cref="SensorType"/> to its canonical string form.</summary>
    /// <param name="type">The sensor type.</param>
    /// <returns>The canonical string form.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a known sensor type.</exception>
    public static string ToName(SensorType type) =>
        NamesByType.TryGetValue(type, out var name)
            ? name
            : throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown sensor type.");

    /// <summary>Converts a canonical string form back to a <see cref="SensorType"/>.</summary>
    /// <param name="name">The canonical string form.</param>
    /// <returns>The matching sensor type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a known sensor type name.</exception>
    public static SensorType FromName(string name) =>
        TryFromName(name, out var type)
            ? type
            : throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown sensor type.");

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
        if (name is null)
        {
            type = default;
            return false;
        }

        return TypesByName.TryGetValue(name, out type);
    }
}
