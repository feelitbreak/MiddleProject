namespace NotificationService.UnitTests.Messaging;

using NotificationService.Messaging;
using System.Text;

/// <summary>
/// Covers the only place this service can reject its input.
/// <para>
/// Every failure here ends with the message dropped rather than retried or dead-lettered, so what
/// matters is that a bad message produces a failure instead of an exception escaping into the
/// consumer loop.
/// </para>
/// </summary>
public sealed class ReadingsPersistedDecoderTests
{
    [Fact]
    public void Decode_ValidPayload_ReturnsTheEvent()
    {
        var result = ReadingsPersistedDecoder.Decode(
            Bytes(
                """
                {"publishedAt":"2026-09-16T09:31:25.481+00:00","readingCount":18,
                 "newestCollectedAt":"2026-09-15T09:31:23.474862+00:00",
                 "sensors":[{"location":"Kitchen","sensorType":"air_quality"},
                            {"location":"Garage","sensorType":"energy"}]}
                """
            )
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(18, result.Value.ReadingCount);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 16, 9, 31, 25, 481, TimeSpan.Zero),
            result.Value.PublishedAt
        );
        Assert.Equal(
            [("Kitchen", "air_quality"), ("Garage", "energy")],
            result.Value.Sensors.Select(sensor => (sensor.Location, sensor.SensorType))
        );
    }

    [Fact]
    public void Decode_MissingSensorArray_ReturnsAnEventWithNoSensors()
    {
        // The event is a signal to refetch, which stays useful without knowing what changed.
        var result = ReadingsPersistedDecoder.Decode(
            Bytes(
                """{"publishedAt":"2026-09-16T09:31:25.481+00:00","readingCount":3,"newestCollectedAt":"2026-09-16T09:31:23.474+00:00"}"""
            )
        );

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Sensors);
    }

    [Fact]
    public void Decode_EmptyBody_ReturnsFailure()
    {
        var result = ReadingsPersistedDecoder.Decode([]);

        Assert.True(result.IsFailure);
        Assert.Contains("empty", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decode_MalformedJson_ReturnsFailureRatherThanThrowing()
    {
        var result = ReadingsPersistedDecoder.Decode(Bytes("{ not json"));

        Assert.True(result.IsFailure);
        Assert.Contains("not valid JSON", result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Decode_JsonNullLiteral_ReturnsFailure()
    {
        var result = ReadingsPersistedDecoder.Decode(Bytes("null"));

        Assert.True(result.IsFailure);
        Assert.Contains("null", result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Decode_MessageFromAnotherTopic_ReturnsAnEventWithDefaults()
    {
        // A meter reading is still an object, so it decodes; the service broadcasts a harmless
        // signal rather than failing. Worth pinning: the consumer must not treat this as a crash.
        var result = ReadingsPersistedDecoder.Decode(
            Bytes("""{"type":"energy","name":"Kitchen","payload":{"energy":12.5}}""")
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.ReadingCount);
        Assert.Empty(result.Value.Sensors);
    }

    private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);
}
