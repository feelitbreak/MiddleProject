namespace DataProcessorService.Infrastructure.Messaging;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// The wire shape of a message on the readings topic:
/// <code>
/// {"type":"energy","name":"Kitchen","payload":{"energy":369.62},"collectedAt":"2026-09-03T10:00:00+00:00"}
/// </code>
/// <para>
/// A deliberate copy of the producer's contract rather than a shared assembly, so the two services
/// stay independently deployable; a contract test pins the JSON so the copy cannot drift. The
/// payload stays a raw <see cref="JsonElement"/> because its shape depends on the sibling
/// discriminator and this service only ever reads it.
/// </para>
/// </summary>
public sealed class MeterReadingMessage
{
    /// <summary>Matches the producer, which serialises with web defaults.</summary>
    public static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>The location the reading came from.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }

    /// <summary>Stamped per poll by the producer; the upstream API has no timestamp.</summary>
    [JsonPropertyName("collectedAt")]
    public DateTimeOffset CollectedAt { get; set; }
}
