namespace GraphQLGatewayService.Domain.Contracts;

using GraphQLGatewayService.Domain.Enums;

/// <summary>A sensor in the catalogue, as exposed by the schema.</summary>
public sealed class Sensor
{
    /// <summary>Gets the surrogate key.</summary>
    public int Id { get; init; }

    /// <summary>Gets the location the sensor reports from.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the kind of data the sensor emits.</summary>
    public SensorType Type { get; init; }
}
