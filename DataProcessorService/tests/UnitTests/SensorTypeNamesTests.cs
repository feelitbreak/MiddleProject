namespace DataProcessorService.UnitTests;

using DataProcessorService.Domain.Enums;

/// <summary>
/// Pins the canonical sensor type vocabulary, which is shared by the wire contract, the
/// <c>sensors.sensor_type</c> column and the table-per-hierarchy discriminator. Changing any of
/// these names silently would break ingestion and every stored row's discriminator at once.
/// <para>
/// The full suite lands in a later change; this covers the one invariant that spans all three.
/// </para>
/// </summary>
public sealed class SensorTypeNamesTests
{
    [Theory]
    [InlineData(SensorType.AirQuality, "air_quality")]
    [InlineData(SensorType.Motion, "motion")]
    [InlineData(SensorType.Energy, "energy")]
    public void ToName_KnownType_ReturnsWireName(SensorType type, string expected) =>
        Assert.Equal(expected, SensorTypeNames.ToName(type));

    [Theory]
    [InlineData("air_quality", SensorType.AirQuality)]
    [InlineData("motion", SensorType.Motion)]
    [InlineData("energy", SensorType.Energy)]
    public void TryFromName_KnownName_ResolvesType(string name, SensorType expected)
    {
        Assert.True(SensorTypeNames.TryFromName(name, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("AirQuality")]
    [InlineData("temperature")]
    [InlineData("")]
    [InlineData(null)]
    public void TryFromName_UnknownName_Fails(string? name) =>
        Assert.False(SensorTypeNames.TryFromName(name, out _));
}
