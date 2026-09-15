namespace GraphQLGatewayService.Domain.Contracts;

using GraphQLGatewayService.Domain.Common;

/// <summary>
/// A validated time range for an aggregation, bounded whether or not the caller supplied one:
/// left open, hourly periods since 1970 would be an unbounded scan.
/// </summary>
public sealed class AggregateWindow
{
    /// <summary>The widest range a single aggregation may span.</summary>
    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(90);

    /// <summary>
    /// The most periods one aggregation may return, per location. Below the 2160 that the widest
    /// range yields hourly, so it constrains rather than sitting unreachable behind
    /// <see cref="MaxRange"/>: that bounds what is scanned, this bounds what comes back.
    /// </summary>
    public const int MaxPeriods = 2_000;

    private AggregateWindow(DateTimeOffset from, DateTimeOffset to)
    {
        this.From = from;
        this.To = to;
    }

    /// <summary>Gets the inclusive lower bound, in UTC.</summary>
    public DateTimeOffset From { get; }

    /// <summary>Gets the exclusive upper bound, in UTC.</summary>
    public DateTimeOffset To { get; }

    /// <summary>Resolves optional bounds against the interval's default window and checks them.</summary>
    public static Result<AggregateWindow> Resolve(
        AggregationInterval interval,
        DateTimeOffset? from,
        DateTimeOffset? to,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var resolvedTo = to ?? timeProvider.GetUtcNow();
        var resolvedFrom = from ?? resolvedTo - DefaultWindow(interval);

        if (resolvedFrom >= resolvedTo)
        {
            return Failure("from must be earlier than to.");
        }

        var range = resolvedTo - resolvedFrom;

        if (range > MaxRange)
        {
            return Failure(
                $"The requested range spans {range.TotalDays:F0} days; at most "
                    + $"{MaxRange.TotalDays:F0} are allowed."
            );
        }

        var periods = EstimatePeriodCount(range, interval);

        if (periods > MaxPeriods)
        {
            return Failure(
                $"The requested range would produce about {periods} periods; at most "
                    + $"{MaxPeriods} are allowed. Widen the interval or narrow the range."
            );
        }

        return Result.Success(
            new AggregateWindow(
                UtcInstant.Normalize(resolvedFrom),
                UtcInstant.Normalize(resolvedTo)
            )
        );
    }

    /// <summary>
    /// How far back to look when given no range: enough for a useful chart without being an
    /// expensive default, so the dashboard renders before any control is touched.
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
        // Approximate on purpose: a guard rail, and week/month boundaries do not divide evenly.
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

    private static Result<AggregateWindow> Failure(string description) =>
        Result.Failure<AggregateWindow>(Error.CreateValidation(description));
}
