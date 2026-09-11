namespace DataProcessorService.Application.Abstractions.Persistence;

using DataProcessorService.Application.Contracts;
using DataProcessorService.Domain.Enums;

/// <summary>Filters applied when listing readings.</summary>
/// <param name="location">Restrict to one location, or null for every location.</param>
/// <param name="sensorType">Restrict to one sensor type, or null for every type.</param>
/// <param name="from">Inclusive lower bound on collection time, or null for unbounded.</param>
/// <param name="to">Exclusive upper bound on collection time, or null for unbounded.</param>
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
/// <param name="metric">The numeric series to aggregate.</param>
/// <param name="interval">How long each period covers.</param>
/// <param name="from">Inclusive lower bound on collection time.</param>
/// <param name="to">Exclusive upper bound on collection time.</param>
/// <param name="location">Restrict to one location, or null for every location.</param>
public sealed class AggregateFilter(
    ReadingMetric metric,
    AggregationInterval interval,
    DateTimeOffset from,
    DateTimeOffset to,
    string? location
)
{
    /// <summary>Gets the numeric series being aggregated.</summary>
    public ReadingMetric Metric { get; } = metric;

    /// <summary>Gets how long each period covers.</summary>
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
    /// <param name="filter">Filters to apply.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>How many readings match.</returns>
    Task<int> CountAsync(ReadingFilter filter, CancellationToken cancellationToken);

    /// <summary>Lists one page of readings, newest first.</summary>
    /// <param name="filter">Filters to apply.</param>
    /// <param name="skip">How many readings to skip.</param>
    /// <param name="take">How many readings to return.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The readings on the requested page.</returns>
    Task<IReadOnlyList<ReadingDto>> ListAsync(
        ReadingFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken
    );

    /// <summary>Returns the most recent reading for every sensor that has one.</summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>One reading per sensor, ordered by location then sensor type.</returns>
    Task<IReadOnlyList<ReadingDto>> ListLatestPerSensorAsync(CancellationToken cancellationToken);

    /// <summary>Aggregates a metric into time periods, grouped by location.</summary>
    /// <param name="filter">Aggregation parameters.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The periods, ordered by time then location.</returns>
    Task<IReadOnlyList<AggregatePeriodDto>> AggregateAsync(
        AggregateFilter filter,
        CancellationToken cancellationToken
    );
}

/// <summary>Read-side access to the sensor catalogue.</summary>
public interface ISensorQueries
{
    /// <summary>Lists every known sensor.</summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The catalogue, ordered by location then sensor type.</returns>
    Task<IReadOnlyList<SensorDto>> ListAsync(CancellationToken cancellationToken);
}
