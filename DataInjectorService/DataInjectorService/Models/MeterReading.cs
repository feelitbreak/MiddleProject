namespace DataInjectorService.Models;

using System.Text.Json;
using System.Text.Json.Serialization;
using DataInjectorService.Helpers;

/// <summary>
/// Represents a single meter reading returned by the WeakApp /meters endpoint.
/// </summary>
public sealed class MeterReading
{
    /// <summary>Gets or sets the sensor type: "air_quality", "motion", or "energy".</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the location name (e.g. "Office", "Kitchen").</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the polymorphic payload, deserialized based on <see cref="Type"/>.</summary>
    [JsonPropertyName("payload")]
    [JsonConverter(typeof(MeterPayloadConverter))]
    public IMeterPayload Payload { get; set; } = new UnknownPayload();

    /// <summary>Gets or sets the UTC timestamp when this reading was collected by the injector.</summary>
    public DateTimeOffset CollectedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Marker interface for all payload variants.</summary>
public interface IMeterPayload { }

/// <summary>Payload for sensors of type "air_quality".</summary>
public sealed class AirQualityPayload : IMeterPayload
{
    /// <summary>Gets or sets CO2 concentration in ppm.</summary>
    [JsonPropertyName("co2")]
    public int Co2 { get; set; }

    /// <summary>Gets or sets fine particulate matter (PM2.5) in µg/m³.</summary>
    [JsonPropertyName("pm25")]
    public int Pm25 { get; set; }

    /// <summary>Gets or sets relative humidity percentage.</summary>
    [JsonPropertyName("humidity")]
    public int Humidity { get; set; }
}

/// <summary>Payload for sensors of type "motion".</summary>
public sealed class MotionPayload : IMeterPayload
{
    /// <summary>Gets or sets a value indicating whether motion was detected.</summary>
    [JsonPropertyName("motionDetected")]
    public bool MotionDetected { get; set; }
}

/// <summary>Payload for sensors of type "energy".</summary>
public sealed class EnergyPayload : IMeterPayload
{
    /// <summary>Gets or sets the energy reading in kWh.</summary>
    [JsonPropertyName("energy")]
    public double Energy { get; set; }
}

/// <summary>Fallback payload used when the sensor type is not recognised.</summary>
public sealed class UnknownPayload : IMeterPayload
{
    /// <summary>Gets or sets the raw JSON of the unrecognised payload for diagnostics.</summary>
    public string RawJson { get; set; } = string.Empty;
}

/// <summary>
/// Intermediate payload that holds the raw <see cref="JsonElement"/> until the type
/// discriminator is available for resolution by <see cref="MeterReadingTypeResolver"/>.
/// </summary>
public sealed class RawJsonPayload : IMeterPayload
{
    /// <summary>Gets or sets the captured JSON element.</summary>
    public JsonElement Element { get; set; }
}

/// <summary>
/// Custom JSON converter that captures the raw payload JSON as a
/// <see cref="RawJsonPayload"/> placeholder during initial deserialization.
/// The correct concrete type is resolved afterwards by
/// <see cref="MeterReadingTypeResolver"/> once the sibling "type" field is available.
/// </summary>
public sealed class MeterPayloadConverter : JsonConverter<IMeterPayload>
{
    /// <inheritdoc/>
    public override IMeterPayload Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return new RawJsonPayload { Element = doc.RootElement.Clone() };
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer,
        IMeterPayload value,
        JsonSerializerOptions options
    )
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
