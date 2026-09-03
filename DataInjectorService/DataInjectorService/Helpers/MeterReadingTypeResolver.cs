namespace DataInjectorService.Helpers;

using System.Text.Json;
using DataInjectorService.Models;

/// <summary>
/// Resolves the polymorphic <see cref="IMeterPayload"/> on a <see cref="MeterReading"/> once
/// the full object has been deserialized and the "type" discriminator is available.
/// </summary>
public static class MeterReadingTypeResolver
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Walks a list of raw readings (which still carry <see cref="RawJsonPayload"/> placeholders)
    /// and replaces each placeholder with the correctly typed concrete payload.
    /// </summary>
    /// <param name="rawReadings">Readings as returned directly from JSON deserialization.</param>
    /// <returns>A new list with all payloads fully resolved.</returns>
    public static IReadOnlyList<MeterReading> Resolve(IEnumerable<MeterReading> rawReadings)
    {
        var result = new List<MeterReading>();

        foreach (var raw in rawReadings)
        {
            if (raw.Payload is not RawJsonPayload rawPayload)
            {
                result.Add(raw);
                continue;
            }

            raw.Payload = ResolvePayload(raw.Type, rawPayload.Element);
            result.Add(raw);
        }

        return result;
    }

    private static IMeterPayload ResolvePayload(string type, JsonElement element) =>
        type switch
        {
            "air_quality" => Deserialize<AirQualityPayload>(element),
            "motion" => Deserialize<MotionPayload>(element),
            "energy" => Deserialize<EnergyPayload>(element),
            _ => new UnknownPayload { RawJson = element.GetRawText() },
        };

    private static IMeterPayload Deserialize<T>(JsonElement element)
        where T : IMeterPayload, new()
    {
        var result = element.Deserialize<T>(PayloadOptions);
        if (result is null)
        {
            return new UnknownPayload { RawJson = element.GetRawText() };
        }

        return result;
    }
}
