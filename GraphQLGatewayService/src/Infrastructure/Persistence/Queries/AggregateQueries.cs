namespace GraphQLGatewayService.Infrastructure.Persistence.Queries;

using GraphQLGatewayService.Domain.Contracts;
using GraphQLGatewayService.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Time-bucketed aggregation over one metric, grouped by location.
/// </summary>
public static class AggregateQueries
{
    /// <summary>
    /// Time zone for <c>date_trunc</c>, explicit so bucket boundaries do not depend on the
    /// server's session time zone.
    /// </summary>
    private const string BucketTimeZone = "UTC";

    /// <summary>
    /// Aggregates one metric into periods, one series per location. A single <c>GROUP BY</c> in
    /// PostgreSQL; only the regrouping into series happens in memory, over a capped result set.
    /// </summary>
    public static async Task<IReadOnlyList<AggregateSeries>> AggregateAsync(
        this MeterReadingsDbContext context,
        ReadingMetric metric,
        AggregationInterval interval,
        AggregateWindow window,
        string? location,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(window);

        var samples = context
            .Samples(metric)
            .Where(sample => sample.CollectedAt >= window.From && sample.CollectedAt < window.To);

        if (!string.IsNullOrWhiteSpace(location))
        {
            var trimmed = location.Trim();
            samples = samples.Where(sample => sample.Location == trimmed);
        }

        var unit = UnitOf(interval);

        var periods = await samples
            .GroupBy(sample => new
            {
                PeriodStart = PostgresFunctions.DateTrunc(unit, sample.CollectedAt, BucketTimeZone),
                sample.Location,
            })
            .Select(group => new
            {
                group.Key.PeriodStart,
                group.Key.Location,
                Count = group.Count(),
                Average = group.Average(sample => sample.Value),
                Minimum = group.Min(sample => sample.Value),
                Maximum = group.Max(sample => sample.Value),
            })
            .OrderBy(period => period.Location)
            .ThenBy(period => period.PeriodStart)
            .ToListAsync(cancellationToken);

        var metricUnit = ReadingMetricUnits.ToUnit(metric);

        return
        [
            .. periods
                .GroupBy(period => period.Location, StringComparer.Ordinal)
                .Select(group => new AggregateSeries
                {
                    Location = group.Key,
                    Unit = metricUnit,
                    Points =
                    [
                        .. group.Select(period => new AggregatePoint
                        {
                            PeriodStart = period.PeriodStart,
                            Count = period.Count,
                            Average = period.Average,
                            Minimum = period.Minimum,
                            Maximum = period.Maximum,
                        }),
                    ],
                }),
        ];
    }

    /// <summary>Maps an interval to its <c>date_trunc</c> unit, bound as a parameter.</summary>
    private static string UnitOf(AggregationInterval interval) =>
        interval switch
        {
            AggregationInterval.Hour => "hour",
            AggregationInterval.Day => "day",
            AggregationInterval.Week => "week",
            AggregationInterval.Month => "month",
            _ => throw new ArgumentOutOfRangeException(
                nameof(interval),
                interval,
                "Unknown aggregation interval."
            ),
        };

    /// <summary>
    /// Reduces one metric to a uniform (time, location, value) series so a single grouping serves
    /// every reading kind. Each branch queries one subtype, applying the discriminator filter.
    /// </summary>
    private static IQueryable<MetricSample> Samples(
        this MeterReadingsDbContext context,
        ReadingMetric metric
    ) =>
        metric switch
        {
            ReadingMetric.Co2 => context
                .Set<AirQualityReadingRow>()
                .Select(reading => new MetricSample
                {
                    CollectedAt = reading.CollectedAt,
                    Location = reading.Sensor!.Name,
                    Value = reading.Co2,
                }),
            ReadingMetric.Pm25 => context
                .Set<AirQualityReadingRow>()
                .Select(reading => new MetricSample
                {
                    CollectedAt = reading.CollectedAt,
                    Location = reading.Sensor!.Name,
                    Value = reading.Pm25,
                }),
            ReadingMetric.Humidity => context
                .Set<AirQualityReadingRow>()
                .Select(reading => new MetricSample
                {
                    CollectedAt = reading.CollectedAt,
                    Location = reading.Sensor!.Name,
                    Value = reading.Humidity,
                }),
            ReadingMetric.MotionDetected => context
                .Set<MotionReadingRow>()
                .Select(reading => new MetricSample
                {
                    CollectedAt = reading.CollectedAt,
                    Location = reading.Sensor!.Name,
                    // As 1 and 0, so that the bucket average is the detection rate.
                    Value = reading.MotionDetected ? 1.0 : 0.0,
                }),
            ReadingMetric.EnergyKwh => context
                .Set<EnergyReadingRow>()
                .Select(reading => new MetricSample
                {
                    CollectedAt = reading.CollectedAt,
                    Location = reading.Sensor!.Name,
                    Value = reading.EnergyKwh,
                }),
            _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown metric."),
        };

    /// <summary>One reading reduced to the series being aggregated.</summary>
    private sealed class MetricSample
    {
        public DateTimeOffset CollectedAt { get; init; }

        public string Location { get; init; } = string.Empty;

        public double Value { get; init; }
    }
}
