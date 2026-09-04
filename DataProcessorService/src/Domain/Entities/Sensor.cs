namespace DataProcessorService.Domain.Entities;

using DataProcessorService.Domain.Enums;

/// <summary>
/// A physical sensor, identified by the location it reports from and the kind of data it emits.
/// <para>
/// The upstream API supplies no sensor identifier, so <see cref="Name"/> and <see cref="Type"/>
/// together form the natural key and are backed by a unique index.
/// </para>
/// </summary>
public sealed class Sensor
{
    /// <summary>Gets or sets the surrogate primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the location the sensor reports from (e.g. "Kitchen", "Office").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the kind of data the sensor emits.</summary>
    public SensorType Type { get; set; }

    /// <summary>Gets or sets the readings recorded by this sensor.</summary>
    public ICollection<MeterReading> Readings { get; set; } = [];
}
