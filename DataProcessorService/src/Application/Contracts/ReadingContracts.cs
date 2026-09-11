namespace DataProcessorService.Application.Contracts;

using DataProcessorService.Domain.Enums;

/// <summary>How long a period covers in an aggregation. Each value is a PostgreSQL truncation unit.</summary>
public enum AggregationInterval
{
    /// <summary>One period per hour.</summary>
    Hour = 1,

    /// <summary>One period per day.</summary>
    Day = 2,

    /// <summary>One period per week.</summary>
    Week = 3,

    /// <summary>One period per month.</summary>
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
    /// Motion, from occupancy sensors, as 1 when detected and 0 otherwise. A period's average is
    /// therefore the fraction of readings in which motion was seen.
    /// </summary>
    MotionDetected = 4,

    /// <summary>Energy consumption in kWh, from energy meters.</summary>
    EnergyKwh = 5,
}

/// <summary>A sensor in the catalogue.</summary>
public sealed class SensorDto(int id, string name, SensorType type)
{
    /// <summary>Gets the surrogate key.</summary>
    public int Id { get; } = id;

    public string Name { get; } = name;

    public SensorType Type { get; } = type;
}

/// <summary>
/// A single reading.
/// <para>
/// Deliberately flat, with the columns that do not apply to the sensor type left null and omitted
/// from the response, rather than a polymorphic payload object. A uniform shape is easier to scan
/// by hand and keeps the generated OpenAPI schema a single type.
/// </para>
/// </summary>
public sealed class ReadingDto
{
    /// <summary>Gets the surrogate key.</summary>
    public long Id { get; init; }

    public string SensorName { get; init; } = string.Empty;

    public SensorType SensorType { get; init; }

    /// <summary>Gets the instant the reading was collected, in UTC.</summary>
    public DateTimeOffset CollectedAt { get; init; }

    /// <summary>Gets the CO2 concentration in ppm, for air quality readings.</summary>
    public int? Co2 { get; init; }

    /// <summary>Gets the PM2.5 concentration in ug/m3, for air quality readings.</summary>
    public int? Pm25 { get; init; }

    /// <summary>Gets the relative humidity percentage, for air quality readings.</summary>
    public int? Humidity { get; init; }

    public bool? MotionDetected { get; init; }

    /// <summary>Gets the energy consumption in kWh, for energy readings.</summary>
    public double? EnergyKwh { get; init; }
}

/// <summary>
/// One period of an aggregation: every reading whose collection time falls between
/// <see cref="PeriodStart"/> and the start of the next period, for one location.
/// </summary>
public sealed class AggregatePeriodDto
{
    /// <summary>Gets the start of the period, in UTC.</summary>
    public DateTimeOffset PeriodStart { get; init; }

    public string Location { get; init; } = string.Empty;

    public int Count { get; init; }

    public double Average { get; init; }

    public double Minimum { get; init; }

    public double Maximum { get; init; }
}

/// <summary>A page of results.</summary>
public sealed class PagedResult<T>(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
{
    public IReadOnlyList<T> Items { get; } = items;

    /// <summary>Gets the one-based page number returned.</summary>
    public int Page { get; } = page;

    public int PageSize { get; } = pageSize;

    public int TotalCount { get; } = totalCount;

    /// <summary>Gets how many pages the filter yields at this page size.</summary>
    public int TotalPages =>
        this.PageSize == 0 ? 0 : (int)Math.Ceiling(this.TotalCount / (double)this.PageSize);

    public bool HasMore => this.Page < this.TotalPages;
}
