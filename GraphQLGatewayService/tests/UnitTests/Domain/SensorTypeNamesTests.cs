namespace GraphQLGatewayService.UnitTests.Domain;

using GraphQLGatewayService.Domain.Enums;

/// <summary>
/// Pins the stored sensor type vocabulary. These spellings are written by DataProcessorService and
/// read here, so a silent change on either side would break every stored row's discriminator.
/// </summary>
public sealed class SensorTypeNamesTests
{
    [Theory]
    [InlineData(SensorType.AirQuality, "air_quality")]
    [InlineData(SensorType.Motion, "motion")]
    [InlineData(SensorType.Energy, "energy")]
    public void ToName_KnownType_ReturnsStoredSpelling(SensorType type, string expected) =>
        Assert.Equal(expected, SensorTypeNames.ToName(type));

    [Theory]
    [InlineData("air_quality", SensorType.AirQuality)]
    [InlineData("motion", SensorType.Motion)]
    [InlineData("energy", SensorType.Energy)]
    public void TryFromName_StoredSpelling_ResolvesType(string name, SensorType expected)
    {
        Assert.True(SensorTypeNames.TryFromName(name, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("AirQuality")]
    [InlineData("AIR_QUALITY")]
    [InlineData("temperature")]
    [InlineData("")]
    [InlineData(null)]
    public void TryFromName_NonCanonicalOrUnknown_Fails(string? name) =>
        Assert.False(SensorTypeNames.TryFromName(name, out _));

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    public void TryFromName_NumericString_Fails(string name) =>
        // Enum.TryParse accepts any numeric string, so without the IsDefined guard a stored "1"
        // would silently read as AirQuality.
        Assert.False(SensorTypeNames.TryFromName(name, out _));

    [Fact]
    public void FromName_UnknownName_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SensorTypeNames.FromName("temperature"));

    [Theory]
    [InlineData(SensorType.AirQuality)]
    [InlineData(SensorType.Motion)]
    [InlineData(SensorType.Energy)]
    public void FromName_RoundTrip_ReturnsOriginal(SensorType type) =>
        Assert.Equal(type, SensorTypeNames.FromName(SensorTypeNames.ToName(type)));
}
