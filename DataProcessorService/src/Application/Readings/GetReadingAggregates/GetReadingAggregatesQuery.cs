namespace DataProcessorService.Application.Readings.GetReadingAggregates;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Contracts;
using DataProcessorService.Domain.Common;

/// <summary>Aggregates one metric into time buckets, grouped by location.</summary>
/// <param name="metric">The numeric series to aggregate.</param>
/// <param name="bucket">How far apart buckets are spaced.</param>
/// <param name="from">Inclusive lower bound on collection time.</param>
/// <param name="to">Exclusive upper bound on collection time.</param>
/// <param name="location">Restrict to one location, or null for every location.</param>
public sealed class GetReadingAggregatesQuery(
    ReadingMetric metric,
    BucketSize bucket,
    DateTimeOffset from,
    DateTimeOffset to,
    string? location
) : IQuery<IReadOnlyList<AggregateBucketDto>>
{
    /// <summary>The widest time range a single aggregation may span.</summary>
    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(90);

    /// <summary>
    /// The most buckets a single aggregation may produce, per location.
    /// <para>
    /// Set below the 2160 buckets that the widest allowed range yields at hourly spacing, so that
    /// this genuinely constrains rather than sitting unreachable behind
    /// <see cref="MaxRange"/>. The two limits guard different things: the range bounds how much is
    /// scanned, this bounds how much comes back.
    /// </para>
    /// </summary>
    public const int MaxBuckets = 2_000;

    /// <summary>Gets the metric being aggregated.</summary>
    public ReadingMetric Metric { get; } = metric;

    /// <summary>Gets the bucket spacing.</summary>
    public BucketSize Bucket { get; } = bucket;

    /// <summary>Gets the inclusive lower bound on collection time.</summary>
    public DateTimeOffset From { get; } = from;

    /// <summary>Gets the exclusive upper bound on collection time.</summary>
    public DateTimeOffset To { get; } = to;

    /// <summary>Gets the location filter.</summary>
    public string? Location { get; } = location;
}

/// <summary>
/// Validates the range, then hands the aggregation to the database.
/// <para>
/// The bounds are mandatory and capped. Without them <c>?bucket=hour&amp;from=1970</c> is a
/// one-parameter denial of service: an unbounded scan producing hundreds of thousands of buckets.
/// </para>
/// </summary>
/// <param name="queries">Read-side access to stored readings.</param>
public sealed class GetReadingAggregatesQueryHandler(IReadingQueries queries)
    : IQueryHandler<GetReadingAggregatesQuery, IReadOnlyList<AggregateBucketDto>>
{
    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<AggregateBucketDto>>> HandleAsync(
        GetReadingAggregatesQuery query,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.From >= query.To)
        {
            return Failure("from must be earlier than to.");
        }

        var range = query.To - query.From;

        if (range > GetReadingAggregatesQuery.MaxRange)
        {
            return Failure(
                $"The requested range spans {range.TotalDays:F0} days; at most "
                    + $"{GetReadingAggregatesQuery.MaxRange.TotalDays:F0} are allowed."
            );
        }

        var bucketCount = EstimateBucketCount(range, query.Bucket);

        if (bucketCount > GetReadingAggregatesQuery.MaxBuckets)
        {
            return Failure(
                $"The requested range would produce about {bucketCount} buckets; at most "
                    + $"{GetReadingAggregatesQuery.MaxBuckets} are allowed. Widen the bucket size "
                    + "or narrow the range."
            );
        }

        var filter = new AggregateFilter(
            query.Metric,
            query.Bucket,
            UtcInstant.Normalize(query.From),
            UtcInstant.Normalize(query.To),
            string.IsNullOrWhiteSpace(query.Location) ? null : query.Location.Trim()
        );

        var buckets = await queries.AggregateAsync(filter, cancellationToken);

        return Result.Success(buckets);
    }

    private static long EstimateBucketCount(TimeSpan range, BucketSize bucket)
    {
        // Approximate on purpose: this is a guard rail, not an exact count, and week and month
        // boundaries do not divide a range evenly.
        var bucketHours = bucket switch
        {
            BucketSize.Hour => 1.0,
            BucketSize.Day => 24.0,
            BucketSize.Week => 24.0 * 7,
            BucketSize.Month => 24.0 * 28,
            _ => 1.0,
        };

        return (long)Math.Ceiling(range.TotalHours / bucketHours);
    }

    private static Result<IReadOnlyList<AggregateBucketDto>> Failure(string description) =>
        Result.Failure<IReadOnlyList<AggregateBucketDto>>(Error.CreateValidation(description));
}
