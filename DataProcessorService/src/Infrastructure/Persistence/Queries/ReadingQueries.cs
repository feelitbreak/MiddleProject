namespace DataProcessorService.Infrastructure.Persistence.Queries;

using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Contracts;
using DataProcessorService.Domain.Entities;
using DataProcessorService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Read-side queries over stored readings, projected straight into response contracts.
/// </summary>
/// <param name="context">The scoped database context.</param>
public sealed class ReadingQueries(MeterReadingsDbContext context) : IReadingQueries
{
    /// <summary>
    /// Time zone passed to <c>date_trunc</c>. Explicit because the function truncates in the
    /// session time zone otherwise, which would make bucket boundaries depend on server
    /// configuration.
    /// </summary>
    private const string BucketTimeZone = "UTC";

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReadingDto>> ListAsync(
        ReadingFilter filter,
        int limit,
        ReadingCursor? after,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Filtered(filter);

        if (after is not null)
        {
            // Written as an explicit disjunction rather than a row-value comparison, which EF
            // cannot translate. The identifier breaks ties within one collection instant, which a
            // whole polling batch shares.
            query = query.Where(reading =>
                reading.CollectedAt < after.CollectedAt
                || (reading.CollectedAt == after.CollectedAt && reading.Id < after.Id)
            );
        }

        return await Project(
                query
                    .OrderByDescending(reading => reading.CollectedAt)
                    .ThenByDescending(reading => reading.Id)
                    // One row beyond the page, so the caller can tell whether more exist without
                    // a second count query.
                    .Take(limit + 1)
            )
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReadingDto>> ListLatestPerSensorAsync(
        CancellationToken cancellationToken
    )
    {
        // A correlated subquery per sensor, which Npgsql turns into a lateral join. The sensor
        // catalogue is small and the unique index on (sensor_id, collected_at) makes each lookup a
        // short backward scan, so this stays cheaper than window functions EF cannot translate.
        var latest = context
            .Sensors.Select(sensor =>
                sensor
                    .Readings.OrderByDescending(reading => reading.CollectedAt)
                    .ThenByDescending(reading => reading.Id)
                    .FirstOrDefault()
            )
            .Where(reading => reading != null)
            .Select(reading => reading!);

        var readings = await Project(latest).ToListAsync(cancellationToken);

        return
        [
            .. readings
                .OrderBy(reading => reading.SensorName, StringComparer.Ordinal)
                .ThenBy(reading => reading.SensorType),
        ];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AggregateBucketDto>> AggregateAsync(
        AggregateFilter filter,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(filter);

        var samples = Samples(filter.Metric)
            .Where(sample => sample.CollectedAt >= filter.From && sample.CollectedAt < filter.To);

        if (filter.Location is not null)
        {
            samples = samples.Where(sample => sample.Location == filter.Location);
        }

        var unit = UnitOf(filter.Bucket);

        var buckets = await samples
            .GroupBy(sample => new
            {
                Bucket = PostgresFunctions.DateTrunc(unit, sample.CollectedAt, BucketTimeZone),
                sample.Location,
            })
            .Select(group => new AggregateBucketDto
            {
                Bucket = group.Key.Bucket,
                Location = group.Key.Location,
                Count = group.Count(),
                Average = group.Average(sample => sample.Value),
                Minimum = group.Min(sample => sample.Value),
                Maximum = group.Max(sample => sample.Value),
            })
            .OrderBy(bucket => bucket.Bucket)
            .ThenBy(bucket => bucket.Location)
            .ToListAsync(cancellationToken);

        return buckets;
    }

    /// <summary>
    /// Maps a bucket size to its <c>date_trunc</c> unit. The result is bound as a parameter, never
    /// concatenated into the statement.
    /// </summary>
    private static string UnitOf(BucketSize bucket) =>
        bucket switch
        {
            BucketSize.Hour => "hour",
            BucketSize.Day => "day",
            BucketSize.Week => "week",
            BucketSize.Month => "month",
            _ => throw new ArgumentOutOfRangeException(
                nameof(bucket),
                bucket,
                "Unknown bucket size."
            ),
        };

    private IQueryable<MeterReading> Filtered(ReadingFilter filter)
    {
        var query = context.MeterReadings.AsNoTracking();

        if (filter.Location is not null)
        {
            query = query.Where(reading => reading.Sensor!.Name == filter.Location);
        }

        if (filter.SensorType is not null)
        {
            query = query.Where(reading => reading.Sensor!.Type == filter.SensorType);
        }

        if (filter.From is not null)
        {
            query = query.Where(reading => reading.CollectedAt >= filter.From);
        }

        if (filter.To is not null)
        {
            query = query.Where(reading => reading.CollectedAt < filter.To);
        }

        return query;
    }

    /// <summary>
    /// Projects the hierarchy into one flat contract.
    /// <para>
    /// EF cannot project into a hierarchy, so the subtype values are read through casts. Under
    /// table-per-hierarchy these translate to plain nullable column reads on the one shared table
    /// --- a single query, no <c>Include</c>, and no per-row follow-up.
    /// </para>
    /// </summary>
    private static IQueryable<ReadingDto> Project(IQueryable<MeterReading> readings) =>
        readings.Select(reading => new ReadingDto
        {
            Id = reading.Id,
            SensorName = reading.Sensor!.Name,
            SensorType = reading.Sensor.Type,
            CollectedAt = reading.CollectedAt,
            Co2 = (int?)(reading as AirQualityReading)!.Co2,
            Pm25 = (int?)(reading as AirQualityReading)!.Pm25,
            Humidity = (int?)(reading as AirQualityReading)!.Humidity,
            MotionDetected = (bool?)(reading as MotionReading)!.MotionDetected,
            EnergyKwh = (double?)(reading as EnergyReading)!.EnergyKwh,
        });

    /// <summary>
    /// Reduces one metric to a uniform (time, location, value) series, so that a single grouping
    /// serves every reading kind. Each branch queries one subtype, which applies the discriminator
    /// filter automatically.
    /// </summary>
    private IQueryable<MetricSample> Samples(ReadingMetric metric) =>
        metric switch
        {
            ReadingMetric.Co2 => context
                .Set<AirQualityReading>()
                .Select(reading => new MetricSample
                {
                    CollectedAt = reading.CollectedAt,
                    Location = reading.Sensor!.Name,
                    Value = reading.Co2,
                }),
            ReadingMetric.Pm25 => context
                .Set<AirQualityReading>()
                .Select(reading => new MetricSample
                {
                    CollectedAt = reading.CollectedAt,
                    Location = reading.Sensor!.Name,
                    Value = reading.Pm25,
                }),
            ReadingMetric.Humidity => context
                .Set<AirQualityReading>()
                .Select(reading => new MetricSample
                {
                    CollectedAt = reading.CollectedAt,
                    Location = reading.Sensor!.Name,
                    Value = reading.Humidity,
                }),
            ReadingMetric.MotionDetected => context
                .Set<MotionReading>()
                .Select(reading => new MetricSample
                {
                    CollectedAt = reading.CollectedAt,
                    Location = reading.Sensor!.Name,
                    // As 1 and 0, so that the bucket average is the detection rate.
                    Value = reading.MotionDetected ? 1.0 : 0.0,
                }),
            ReadingMetric.EnergyKwh => context
                .Set<EnergyReading>()
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
