namespace DataInjectorService.Tests.Models;

using DataInjectorService.Helpers;
using DataInjectorService.Models;
using System.Text.Json;

/// <summary>
/// Tests that <see cref="MeterReadingTypeResolver"/> correctly resolves each payload variant
/// and handles unknown / malformed types gracefully.
/// </summary>
public sealed class MeterReadingTypeResolverTests
{
    [Fact]
    public void Resolve_AirQualityType_ReturnsAirQualityPayload()
    {
        var raw = BuildRaw("air_quality", """{"co2":794,"pm25":25,"humidity":70}""");

        var resolved = MeterReadingTypeResolver.Resolve([raw]);

        var payload = Assert.IsType<AirQualityPayload>(resolved[0].Payload);
        Assert.Equal(794, payload.Co2);
        Assert.Equal(25, payload.Pm25);
        Assert.Equal(70, payload.Humidity);
    }

    [Fact]
    public void Resolve_MotionType_ReturnsMotionPayload()
    {
        var raw = BuildRaw("motion", """{"motionDetected":true}""");

        var resolved = MeterReadingTypeResolver.Resolve([raw]);

        var payload = Assert.IsType<MotionPayload>(resolved[0].Payload);
        Assert.True(payload.MotionDetected);
    }

    [Fact]
    public void Resolve_MotionFalse_ReturnsMotionPayloadWithFalse()
    {
        var raw = BuildRaw("motion", """{"motionDetected":false}""");

        var resolved = MeterReadingTypeResolver.Resolve([raw]);

        var payload = Assert.IsType<MotionPayload>(resolved[0].Payload);
        Assert.False(payload.MotionDetected);
    }

    [Fact]
    public void Resolve_EnergyType_ReturnsEnergyPayload()
    {
        var raw = BuildRaw("energy", """{"energy":227.64}""");

        var resolved = MeterReadingTypeResolver.Resolve([raw]);

        var payload = Assert.IsType<EnergyPayload>(resolved[0].Payload);
        Assert.Equal(227.64, payload.Energy, precision: 2);
    }

    [Fact]
    public void Resolve_UnknownType_ReturnsUnknownPayloadWithRawJson()
    {
        var raw = BuildRaw("temperature", """{"celsius":22}""");

        var resolved = MeterReadingTypeResolver.Resolve([raw]);

        var payload = Assert.IsType<UnknownPayload>(resolved[0].Payload);
        Assert.False(string.IsNullOrEmpty(payload.RawJson));
    }

    [Fact]
    public void Resolve_AlreadyResolvedPayload_PassesThroughUnchanged()
    {
        var reading = new MeterReading
        {
            Type = "energy",
            Name = "Hall",
            Payload = new EnergyPayload { Energy = 100.0 },
        };

        var resolved = MeterReadingTypeResolver.Resolve([reading]);

        Assert.IsType<EnergyPayload>(resolved[0].Payload);
    }

    [Fact]
    public void Resolve_MultipleReadings_AllResolved()
    {
        var raws = new[]
        {
            BuildRaw("air_quality", """{"co2":400,"pm25":10,"humidity":50}"""),
            BuildRaw("motion",      """{"motionDetected":false}"""),
            BuildRaw("energy",      """{"energy":99.9}"""),
        };

        var resolved = MeterReadingTypeResolver.Resolve(raws);

        Assert.Equal(3, resolved.Count);
        Assert.IsType<AirQualityPayload>(resolved[0].Payload);
        Assert.IsType<MotionPayload>(resolved[1].Payload);
        Assert.IsType<EnergyPayload>(resolved[2].Payload);
    }

    [Fact]
    public void Resolve_EmptyList_ReturnsEmptyList()
    {
        var resolved = MeterReadingTypeResolver.Resolve([]);

        Assert.Empty(resolved);
    }

    [Fact]
    public void Resolve_PreservesNameAndType()
    {
        var raw = BuildRaw("energy", """{"energy":500}""");

        var resolved = MeterReadingTypeResolver.Resolve([raw]);

        Assert.Equal("energy", resolved[0].Type);
        Assert.Equal("TestRoom", resolved[0].Name);
    }

    private static MeterReading BuildRaw(string type, string payloadJson)
    {
        var element = JsonDocument.Parse(payloadJson).RootElement.Clone();
        return new MeterReading
        {
            Type = type,
            Name = "TestRoom",
            Payload = new RawJsonPayload { Element = element },
        };
    }
}
