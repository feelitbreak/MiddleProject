namespace DataProcessorService.Infrastructure.Messaging;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// The wire shape of a message on the readings topic, as published by DataInjectorService:
/// <code>
/// {"type":"energy","name":"Kitchen","payload":{"energy":369.62},"collectedAt":"2026-09-03T10:00:00+00:00"}
/// </code>
/// <para>
/// Deliberately a copy of the producer's contract rather than a shared assembly, so the two
/// services stay independently deployable. A contract test pins this exact JSON so the copy cannot
/// drift unnoticed.
/// </para>
/// <para>
/// <c>payload</c> stays a raw <see cref="JsonElement"/>: its shape depends on the sibling
/// <c>type</c> discriminator, and unlike the producer this service only ever reads it, so a
/// two-phase converter would be needless machinery.
/// </para>
/// </summary>
public sealed class MeterReadingMessage
{
    /// <summary>Serializer settings matching the producer's, which uses web defaults.</summary>
    public static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    /// <summary>Gets or sets the sensor type discriminator: air_quality, motion or energy.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Gets or sets the location the reading came from.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Gets or sets the type-dependent payload.</summary>
    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }

    /// <summary>
    /// Gets or sets the instant the injector collected the reading. The upstream API supplies no
    /// timestamp, so this is stamped per poll by the producer.
    /// </summary>
    [JsonPropertyName("collectedAt")]
    public DateTimeOffset CollectedAt { get; set; }
}
