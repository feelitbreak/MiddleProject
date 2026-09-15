namespace GraphQLGatewayService.UnitTests.Domain;

using GraphQLGatewayService.Domain.Contracts;

/// <summary>
/// The unit travels to the client on every aggregation, so a missing case would surface as a
/// mislabelled chart axis rather than an obvious failure.
/// </summary>
public sealed class ReadingMetricUnitsTests
{
    [Theory]
    [InlineData(ReadingMetric.Co2, "ppm")]
    [InlineData(ReadingMetric.Pm25, "ug/m3")]
    [InlineData(ReadingMetric.Humidity, "%")]
    [InlineData(ReadingMetric.MotionDetected, "fraction")]
    [InlineData(ReadingMetric.EnergyKwh, "kWh")]
    public void ToUnit_KnownMetric_ReturnsUnit(ReadingMetric metric, string expected) =>
        Assert.Equal(expected, ReadingMetricUnits.ToUnit(metric));

    [Fact]
    public void ToUnit_EveryDeclaredMetric_IsCovered() =>
        Assert.All(
            Enum.GetValues<ReadingMetric>(),
            metric => Assert.False(string.IsNullOrWhiteSpace(ReadingMetricUnits.ToUnit(metric)))
        );

    [Fact]
    public void ToUnit_UndeclaredMetric_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ReadingMetricUnits.ToUnit((ReadingMetric)99));
}
