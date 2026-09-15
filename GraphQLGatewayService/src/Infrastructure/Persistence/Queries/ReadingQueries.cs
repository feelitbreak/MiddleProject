namespace GraphQLGatewayService.Infrastructure.Persistence.Queries;

using GraphQLGatewayService.Domain.Contracts;
using GraphQLGatewayService.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Composable read queries. Each returns an <see cref="IQueryable{T}"/> so a resolver can append
/// ordering and paging and still execute one statement. Tracking is off context-wide.
/// </summary>
public static class ReadingQueries
{
    /// <summary>Applies the optional filter to the full readings table.</summary>
    public static IQueryable<MeterReadingRow> FilterReadings(
        this MeterReadingsDbContext context,
        ReadingFilter? filter
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        var query = context.MeterReadings.AsQueryable();

        if (filter is null)
        {
            return query;
        }

        if (!string.IsNullOrWhiteSpace(filter.Location))
        {
            var location = filter.Location.Trim();
            query = query.Where(reading => reading.Sensor!.Name == location);
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
    /// Projects the hierarchy into one flat contract. The subtype casts become plain nullable
    /// column reads under table-per-hierarchy, so this stays a single query.
    /// <para>
    /// <see cref="Reading.Id"/> and <see cref="Reading.CollectedAt"/> must stay direct member
    /// assignments, or keyset paging cannot read the ordering keys off the projection.
    /// </para>
    /// </summary>
    public static IQueryable<Reading> ProjectToReading(this IQueryable<MeterReadingRow> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        return readings.Select(reading => new Reading
        {
            Id = reading.Id,
            Sensor = new Sensor
            {
                Id = reading.Sensor!.Id,
                Name = reading.Sensor.Name,
                Type = reading.Sensor.Type,
            },
            CollectedAt = reading.CollectedAt,
            Co2 = (int?)(reading as AirQualityReadingRow)!.Co2,
            Pm25 = (int?)(reading as AirQualityReadingRow)!.Pm25,
            Humidity = (int?)(reading as AirQualityReadingRow)!.Humidity,
            MotionDetected = (bool?)(reading as MotionReadingRow)!.MotionDetected,
            EnergyKwh = (double?)(reading as EnergyReadingRow)!.EnergyKwh,
        });
    }

    /// <summary>
    /// The most recent reading for every sensor the filter admits. A correlated subquery per
    /// sensor, which Npgsql turns into a lateral join over the (sensor_id, collected_at) index.
    /// </summary>
    public static IQueryable<MeterReadingRow> LatestPerSensor(
        this MeterReadingsDbContext context,
        ReadingFilter? filter
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        var sensors = context.Sensors.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter?.Location))
        {
            var location = filter.Location.Trim();
            sensors = sensors.Where(sensor => sensor.Name == location);
        }

        if (filter?.SensorType is not null)
        {
            sensors = sensors.Where(sensor => sensor.Type == filter.SensorType);
        }

        // Tested inside the subquery because they constrain the correlated collection, not the
        // outer sequence. EF parameterises the captured locals.
        var from = filter?.From;
        var to = filter?.To;

        return sensors
            .Select(sensor =>
                sensor
                    .Readings.Where(reading =>
                        (from == null || reading.CollectedAt >= from)
                        && (to == null || reading.CollectedAt < to)
                    )
                    .OrderByDescending(reading => reading.CollectedAt)
                    .ThenByDescending(reading => reading.Id)
                    .FirstOrDefault()
            )
            .Where(reading => reading != null)
            .Select(reading => reading!);
    }

    /// <summary>Every distinct location, ordered, for a filter control.</summary>
    public static IQueryable<string> Locations(this MeterReadingsDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Sensors.Select(sensor => sensor.Name).Distinct().OrderBy(name => name);
    }
}
