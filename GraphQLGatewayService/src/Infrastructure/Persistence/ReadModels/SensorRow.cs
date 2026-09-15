namespace GraphQLGatewayService.Infrastructure.Persistence.ReadModels;

using GraphQLGatewayService.Domain.Enums;

/// <summary>
/// A row of the <c>sensors</c> table, which DataProcessorService owns and migrates. The upstream
/// API supplies no identifier, so <see cref="Name"/> and <see cref="Type"/> are the natural key.
/// </summary>
public sealed class SensorRow
{
    public int Id { get; set; }

    /// <summary>Gets or sets the location the sensor reports from.</summary>
    public string Name { get; set; } = string.Empty;

    public SensorType Type { get; set; }

    public ICollection<MeterReadingRow> Readings { get; } = [];
}
