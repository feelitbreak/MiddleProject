namespace DataProcessorService.UnitTests.Messaging;

using DataProcessorService.Domain.Common;
using DataProcessorService.Domain.Enums;
using DataProcessorService.Infrastructure.Messaging;
using System.Text;

/// <summary>
/// Covers decoding of the readings topic's wire format.
/// <para>
/// The first test is a contract pin. The message shape is duplicated from DataInjectorService
/// rather than shared through an assembly, so that the two services stay independently deployable;
/// the cost of that choice is that nothing but a test stops the copy drifting from the producer.
/// </para>
/// <para>
/// Every failure here must be permanent rather than transient: the consumer dead-letters permanent
/// failures and retries transient ones, so misclassifying a malformed message would wedge the
/// partition retrying something that can never succeed.
/// </para>
/// </summary>
public sealed class MeterReadingMessageDecoderTests
{
    [Fact]
    public void Decode_ProducerWireFormat_IsUnderstood()
    {
        // Verbatim from DataInjectorService's KafkaProducer: web-defaults camelCase, with
        // collectedAt stamped by the producer because the upstream API has no timestamp.
        const string Json = """
            {"type":"energy","name":"Kitchen","payload":{"energy":369.62},"collectedAt":"2026-09-03T10:00:00+00:00"}
            """;

        var result = Decode(Json);

        Assert.True(result.IsSuccess);
        Assert.Equal("Kitchen", result.Value.SensorName);
        Assert.Equal(SensorType.Energy, result.Value.SensorType);
        Assert.Equal(369.62, result.Value.Values.EnergyKwh);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero),
            result.Value.CollectedAt
        );
    }

    [Fact]
    public void Decode_AirQualityPayload_ReadsEveryField()
    {
        const string Json = """
            {"type":"air_quality","name":"Office","payload":{"co2":415,"pm25":7,"humidity":44},"collectedAt":"2026-09-03T10:00:00+00:00"}
            """;

        var result = Decode(Json);

        Assert.True(result.IsSuccess);
        Assert.Equal(SensorType.AirQuality, result.Value.SensorType);
        Assert.Equal(415, result.Value.Values.Co2);
        Assert.Equal(7, result.Value.Values.Pm25);
        Assert.Equal(44, result.Value.Values.Humidity);
        Assert.Null(result.Value.Values.EnergyKwh);
        Assert.Null(result.Value.Values.MotionDetected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Decode_MotionPayload_ReadsBothStates(bool detected)
    {
        var json = $$"""
            {"type":"motion","name":"Garage","payload":{"motionDetected":{{(detected ? "true" : "false")}}},"collectedAt":"2026-09-03T10:00:00+00:00"}
            """;

        var result = Decode(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(detected, result.Value.Values.MotionDetected);
    }

    [Fact]
    public void Decode_UnknownType_FailsPermanently()
    {
        // The producer forwards unrecognised sensor types rather than dropping them, so this is a
        // shape the consumer genuinely receives.
        const string Json = """
            {"type":"temperature","name":"Attic","payload":{"celsius":21},"collectedAt":"2026-09-03T10:00:00+00:00"}
            """;

        AssertPermanentFailure(Decode(Json), "temperature");
    }

    [Fact]
    public void Decode_MalformedJson_FailsPermanently() =>
        AssertPermanentFailure(Decode("{\"type\":\"energy\","), "valid JSON");

    [Fact]
    public void Decode_EmptyBody_FailsPermanently() =>
        AssertPermanentFailure(Decode(string.Empty), "empty");

    [Fact]
    public void Decode_JsonNullBody_FailsPermanently() =>
        AssertPermanentFailure(Decode("null"), "null");

    [Fact]
    public void Decode_MissingName_FailsPermanently()
    {
        const string Json = """
            {"type":"energy","payload":{"energy":1.0},"collectedAt":"2026-09-03T10:00:00+00:00"}
            """;

        AssertPermanentFailure(Decode(Json), "sensor name");
    }

    [Fact]
    public void Decode_PayloadMissingFields_FailsPermanently()
    {
        const string Json = """
            {"type":"air_quality","name":"Office","payload":{"co2":415},"collectedAt":"2026-09-03T10:00:00+00:00"}
            """;

        AssertPermanentFailure(Decode(Json), "co2, pm25 and humidity");
    }

    [Fact]
    public void Decode_PayloadWrongJsonType_FailsPermanently()
    {
        const string Json = """
            {"type":"motion","name":"Garage","payload":{"motionDetected":"yes"},"collectedAt":"2026-09-03T10:00:00+00:00"}
            """;

        AssertPermanentFailure(Decode(Json), "boolean motionDetected");
    }

    [Fact]
    public void Decode_PayloadNotAnObject_FailsPermanently()
    {
        // Under fault injection the upstream API returns an error body in place of readings. The
        // producer filters those out, but a payload that is a scalar rather than an object must
        // still fail cleanly here rather than throw.
        const string Json = """
            {"type":"energy","name":"Kitchen","payload":12.5,"collectedAt":"2026-09-03T10:00:00+00:00"}
            """;

        AssertPermanentFailure(Decode(Json), "expected an object");
    }

    [Fact]
    public void Decode_NonUtf8Bytes_FailsPermanentlyRatherThanThrowing()
    {
        // The consumer reads byte[] precisely so that undecodable bytes reach this method and can
        // be dead-lettered, instead of throwing inside Consume() where the payload is unreachable.
        var invalid = new byte[] { 0xC3, 0x28, 0xA0, 0xA1 };

        var result = MeterReadingMessageDecoder.Decode(invalid);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Permanent, result.Error.Code);
    }

    [Fact]
    public void Decode_OffsetTimestamp_IsPreservedForLaterNormalization()
    {
        const string Json = """
            {"type":"energy","name":"Kitchen","payload":{"energy":1.0},"collectedAt":"2026-09-03T12:00:00+02:00"}
            """;

        var result = Decode(Json);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero),
            result.Value.CollectedAt.ToUniversalTime()
        );
    }

    private static Result<Application.Readings.IngestReadingBatch.ReadingToIngest> Decode(
        string json
    ) => MeterReadingMessageDecoder.Decode(Encoding.UTF8.GetBytes(json));

    private static void AssertPermanentFailure<T>(Result<T> result, string expectedInDescription)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Permanent, result.Error.Code);
        Assert.False(result.Error.IsRetryable);
        Assert.Contains(
            expectedInDescription,
            result.Error.Description,
            StringComparison.OrdinalIgnoreCase
        );
    }
}
