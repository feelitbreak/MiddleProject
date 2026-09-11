namespace DataProcessorService.Application.Readings.GetReadingAggregates;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Contracts;
using DataProcessorService.Domain.Common;

/// <summary>
/// Aggregates one metric into time periods, grouped by location.
/// <para>
/// <paramref name="from"/> and <paramref name="to"/> are optional and default to the last day, so
/// that the endpoint is useful with nothing but a metric.
/// </para>
/// </summary>
/// <param name="metric">The numeric series to aggregate.</param>
/// <param name="interval">How long each period covers.</param>
/// <param name="from">Inclusive lower bound, or null for one interval-appropriate window back.</param>
/// <param name="to">Exclusive upper bound, or null for now.</param>
/// <param name="location">Restrict to one location, or null for every location.</param>
public sealed class GetReadingAggregatesQuery(
    ReadingMetric metric,
    AggregationInterval interval,
    DateTimeOffset? from,
    DateTimeOffset? to,
    string? location
) : IQuery<IReadOnlyList<AggregatePeriodDto>>
{
    /// <summary>The widest time range a single aggregation may span.</summary>
    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(90);

    /// <summary>
    /// The most periods a single aggregation may return, per location.
    /// <para>
    /// Set below the 2160 periods that the widest allowed range yields at hourly spacing, so that
    /// this genuinely constrains rather than sitting unreachable behind <see cref="MaxRange"/>.
    /// The two limits guard different things: the range bounds how much is scanned, this bounds
    /// how much comes back.
    /// </para>
    /// </summary>
    public const int MaxPeriods = 2_000;

    /// <summary>Gets the metric being aggregated.</summary>
    public ReadingMetric Metric { get; } = metric;

    /// <summary>Gets how long each period covers.</summary>
    public AggregationInterval Interval { get; } = interval;

    /// <summary>Gets the inclusive lower bound, if the caller supplied one.</summary>
    public DateTimeOffset? From { get; } = from;

    /// <summary>Gets the exclusive upper bound, if the caller supplied one.</summary>
    public DateTimeOffset? To { get; } = to;

    /// <summary>Gets the location filter.</summary>
    public string? Location { get; } = location;
}

/// <summary>
/// Resolves the time range, checks it is within bounds, then hands the aggregation to the database.
/// <para>
/// The range is always bounded, whether the caller supplied it or not. Left open, a request for
/// hourly periods since 1970 would be an unbounded scan returning hundreds of thousands of rows.
/// </para>
/// </summary>
/// <param name="queries">Read-side access to stored readings.</param>
/// <param name="timeProvider">Clock used to resolve a default upper bound.</param>
public sealed class GetReadingAggregatesQueryHandler(
    IReadingQueries queries,
    TimeProvider timeProvider
) : IQueryHandler<GetReadingAggregatesQuery, IReadOnlyList<AggregatePeriodDto>>
{
    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<AggregatePeriodDto>>> HandleAsync(
        GetReadingAggregatesQuery query,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var to = query.To ?? timeProvider.GetUtcNow();
        var from = query.From ?? to - DefaultWindow(query.Interval);

        if (from >= to)
        {
            return Failure("from must be earlier than to.");
        }

        var range = to - from;

        if (range > GetReadingAggregatesQuery.MaxRange)
        {
            return Failure(
                $"The requested range spans {range.TotalDays:F0} days; at most "
                    + $"{GetReadingAggregatesQuery.MaxRange.TotalDays:F0} are allowed."
            );
        }

        var periods = EstimatePeriodCount(range, query.Interval);

        if (periods > GetReadingAggregatesQuery.MaxPeriods)
        {
            return Failure(
                $"The requested range would produce about {periods} periods; at most "
                    + $"{GetReadingAggregatesQuery.MaxPeriods} are allowed. Widen the interval or "
                    + "narrow the range."
            );
        }

        var filter = new AggregateFilter(
            query.Metric,
            query.Interval,
            UtcInstant.Normalize(from),
            UtcInstant.Normalize(to),
            string.IsNullOrWhiteSpace(query.Location) ? null : query.Location.Trim()
        );

        return Result.Success(await queries.AggregateAsync(filter, cancellationToken));
    }

    /// <summary>
    /// How far back to look when the caller gives no range: enough periods to be a useful chart
    /// without being an expensive default.
    /// </summary>
    private static TimeSpan DefaultWindow(AggregationInterval interval) =>
        interval switch
        {
            AggregationInterval.Hour => TimeSpan.FromDays(1),
            AggregationInterval.Day => TimeSpan.FromDays(30),
            AggregationInterval.Week => TimeSpan.FromDays(90),
            AggregationInterval.Month => TimeSpan.FromDays(90),
            _ => TimeSpan.FromDays(1),
        };

    private static long EstimatePeriodCount(TimeSpan range, AggregationInterval interval)
    {
        // Approximate on purpose: this is a guard rail, not an exact count, and week and month
        // boundaries do not divide a range evenly.
        var hours = interval switch
        {
            AggregationInterval.Hour => 1.0,
            AggregationInterval.Day => 24.0,
            AggregationInterval.Week => 24.0 * 7,
            AggregationInterval.Month => 24.0 * 28,
            _ => 1.0,
        };

        return (long)Math.Ceiling(range.TotalHours / hours);
    }

    private static Result<IReadOnlyList<AggregatePeriodDto>> Failure(string description) =>
        Result.Failure<IReadOnlyList<AggregatePeriodDto>>(Error.CreateValidation(description));
}
