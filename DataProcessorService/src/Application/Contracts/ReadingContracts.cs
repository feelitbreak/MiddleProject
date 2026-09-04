namespace DataProcessorService.Application.Contracts;

using DataProcessorService.Domain.Enums;

/// <summary>
/// How far apart aggregation buckets are spaced. Values map to PostgreSQL <c>date_trunc</c> units.
/// <para>
/// An enum rather than a free string on purpose: the unit reaches <c>date_trunc</c>, and although
/// it is bound as a parameter rather than concatenated, restricting the accepted values to a fixed
/// set means model binding rejects anything unexpected before a handler ever runs.
/// </para>
/// </summary>
public enum BucketSize
{
    /// <summary>One bucket per hour.</summary>
    Hour = 1,

    /// <summary>One bucket per day.</summary>
    Day = 2,

    /// <summary>One bucket per week.</summary>
    Week = 3,

    /// <summary>One bucket per month.</summary>
    Month = 4,
}

/// <summary>
/// The numeric series being aggregated. Each value implies the sensor type it belongs to, which is
/// what lets one aggregation endpoint serve every reading kind.
/// </summary>
public enum ReadingMetric
{
    /// <summary>CO2 concentration in ppm, from air quality sensors.</summary>
    Co2 = 1,

    /// <summary>PM2.5 concentration in ug/m3, from air quality sensors.</summary>
    Pm25 = 2,

    /// <summary>Relative humidity percentage, from air quality sensors.</summary>
    Humidity = 3,

    /// <summary>
    /// Motion, from occupancy sensors, as 1 when detected and 0 otherwise. The average over a
    /// bucket is therefore the fraction of readings in which motion was seen.
    /// </summary>
    MotionDetected = 4,

    /// <summary>Energy consumption in kWh, from energy meters.</summary>
    EnergyKwh = 5,
}

/// <summary>A sensor in the catalogue.</summary>
/// <param name="id">Surrogate key.</param>
/// <param name="name">The location the sensor reports from.</param>
/// <param name="type">The kind of data it emits.</param>
public sealed class SensorDto(int id, string name, SensorType type)
{
    /// <summary>Gets the surrogate key.</summary>
    public int Id { get; } = id;

    /// <summary>Gets the location the sensor reports from.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the kind of data the sensor emits.</summary>
    public SensorType Type { get; } = type;
}

/// <summary>
/// A single reading.
/// <para>
/// Deliberately flat, with the columns that do not apply to the sensor type left null and omitted
/// from the response, rather than a polymorphic payload object. Readings are consumed here for
/// charting and tabulation, where a uniform shape is easier to work with, and it keeps the
/// generated OpenAPI schema a single type.
/// </para>
/// </summary>
public sealed class ReadingDto
{
    /// <summary>Gets the surrogate key.</summary>
    public long Id { get; init; }

    /// <summary>Gets the location the reading came from.</summary>
    public string SensorName { get; init; } = string.Empty;

    /// <summary>Gets the kind of sensor that produced the reading.</summary>
    public SensorType SensorType { get; init; }

    /// <summary>Gets the instant the reading was collected, in UTC.</summary>
    public DateTimeOffset CollectedAt { get; init; }

    /// <summary>Gets the CO2 concentration in ppm, for air quality readings.</summary>
    public int? Co2 { get; init; }

    /// <summary>Gets the PM2.5 concentration in ug/m3, for air quality readings.</summary>
    public int? Pm25 { get; init; }

    /// <summary>Gets the relative humidity percentage, for air quality readings.</summary>
    public int? Humidity { get; init; }

    /// <summary>Gets a value indicating whether motion was detected, for motion readings.</summary>
    public bool? MotionDetected { get; init; }

    /// <summary>Gets the energy consumption in kWh, for energy readings.</summary>
    public double? EnergyKwh { get; init; }
}

/// <summary>One aggregation bucket.</summary>
public sealed class AggregateBucketDto
{
    /// <summary>Gets the start of the bucket, in UTC.</summary>
    public DateTimeOffset Bucket { get; init; }

    /// <summary>Gets the location the readings came from.</summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>Gets how many readings fell into the bucket.</summary>
    public int Count { get; init; }

    /// <summary>Gets the mean value over the bucket.</summary>
    public double Average { get; init; }

    /// <summary>Gets the smallest value in the bucket.</summary>
    public double Minimum { get; init; }

    /// <summary>Gets the largest value in the bucket.</summary>
    public double Maximum { get; init; }
}

/// <summary>
/// A page of results, addressed by cursor rather than by page number.
/// <para>
/// Readings arrive continuously at the head of the sort, so an offset-based page 2 would both
/// re-scan everything before it and shift under the reader between requests. A cursor also maps
/// directly onto the connection model a GraphQL gateway will want. The trade-off is that there is
/// no "page 5 of 20" --- only "next".
/// </para>
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="items">The items on this page.</param>
/// <param name="nextCursor">Cursor for the following page, or null when this is the last page.</param>
public sealed class CursorPage<T>(IReadOnlyList<T> items, string? nextCursor)
{
    /// <summary>Gets the items on this page.</summary>
    public IReadOnlyList<T> Items { get; } = items;

    /// <summary>Gets the cursor for the following page, or null when this is the last page.</summary>
    public string? NextCursor { get; } = nextCursor;

    /// <summary>Gets a value indicating whether a following page exists.</summary>
    public bool HasMore => this.NextCursor is not null;
}
