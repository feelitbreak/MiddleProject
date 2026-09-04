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
/// <param name="bucket">How far apart buckets are spaced.</param>
/// <param name="from">Inclusive lower bound on collection time.</param>
/// <param name="to">Exclusive upper bound on collection time.</param>
/// <param name="location">Restrict to one location, or null for every location.</param>
public sealed class AggregateFilter(
    ReadingMetric metric,
    BucketSize bucket,
    DateTimeOffset from,
    DateTimeOffset to,
    string? location
)
{
    /// <summary>Gets the numeric series being aggregated.</summary>
    public ReadingMetric Metric { get; } = metric;

    /// <summary>Gets the bucket spacing.</summary>
    public BucketSize Bucket { get; } = bucket;

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
    /// <summary>Lists readings newest-first, starting after <paramref name="after"/>.</summary>
    /// <param name="filter">Filters to apply.</param>
    /// <param name="limit">Maximum number of readings to return.</param>
    /// <param name="after">Position to resume from, or null to start at the newest reading.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// Up to <paramref name="limit"/> plus one readings. The extra item, when present, tells the
    /// caller another page exists without a second count query.
    /// </returns>
    Task<IReadOnlyList<ReadingDto>> ListAsync(
        ReadingFilter filter,
        int limit,
        ReadingCursor? after,
        CancellationToken cancellationToken
    );

    /// <summary>Returns the most recent reading for every sensor that has one.</summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>One reading per sensor, ordered by location then sensor type.</returns>
    Task<IReadOnlyList<ReadingDto>> ListLatestPerSensorAsync(CancellationToken cancellationToken);

    /// <summary>Aggregates a metric into time buckets, grouped by location.</summary>
    /// <param name="filter">Aggregation parameters.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The buckets, ordered by time then location.</returns>
    Task<IReadOnlyList<AggregateBucketDto>> AggregateAsync(
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
