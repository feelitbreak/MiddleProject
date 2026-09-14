namespace DataProcessorService.Application.Abstractions.Persistence;

using DataProcessorService.Application.Contracts;
using DataProcessorService.Domain.Enums;

/// <summary>Filters applied when listing readings.</summary>
public sealed class ReadingFilter(
    string? location,
    SensorType? sensorType,
    DateTimeOffset? from,
    DateTimeOffset? to
)
{
    /// <summary>Gets the location filter, or null for every location.</summary>
    public string? Location { get; } = location;

    /// <summary>Gets the sensor type filter, or null for every type.</summary>
    public SensorType? SensorType { get; } = sensorType;

    /// <summary>Gets the inclusive lower bound on collection time.</summary>
    public DateTimeOffset? From { get; } = from;

    /// <summary>Gets the exclusive upper bound on collection time.</summary>
    public DateTimeOffset? To { get; } = to;
}

/// <summary>Parameters for a bucketed aggregation.</summary>
public sealed class AggregateFilter(
    ReadingMetric metric,
    AggregationInterval interval,
    DateTimeOffset from,
    DateTimeOffset to,
    string? location
)
{
    public ReadingMetric Metric { get; } = metric;

    public AggregationInterval Interval { get; } = interval;

    /// <summary>Gets the inclusive lower bound on collection time.</summary>
    public DateTimeOffset From { get; } = from;

    /// <summary>Gets the exclusive upper bound on collection time.</summary>
    public DateTimeOffset To { get; } = to;

    /// <summary>Gets the location filter, or null for every location.</summary>
    public string? Location { get; } = location;
}

/// <summary>
/// Read-side access to stored readings.
/// <para>
/// Separate from <see cref="IMeterReadingRepository"/> deliberately: the write side deals in rows
/// shaped for one idempotent insert, while the read side projects straight into response contracts
/// and never materialises an entity it does not need.
/// </para>
/// </summary>
public interface IReadingQueries
{
    /// <summary>Counts the readings matching a filter.</summary>
    Task<int> CountAsync(ReadingFilter filter, CancellationToken cancellationToken);

    /// <summary>Lists one page of readings, newest first.</summary>
    Task<IReadOnlyList<ReadingDto>> ListAsync(
        ReadingFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken
    );

    /// <summary>Returns the most recent reading for every sensor that has one.</summary>
    Task<IReadOnlyList<ReadingDto>> ListLatestPerSensorAsync(CancellationToken cancellationToken);

    /// <summary>Aggregates a metric into time periods, grouped by location.</summary>
    Task<IReadOnlyList<AggregatePeriodDto>> AggregateAsync(
        AggregateFilter filter,
        CancellationToken cancellationToken
    );
}

/// <summary>Read-side access to the sensor catalogue.</summary>
public interface ISensorQueries
{
    /// <summary>Lists every known sensor.</summary>
    Task<IReadOnlyList<SensorDto>> ListAsync(CancellationToken cancellationToken);
}
