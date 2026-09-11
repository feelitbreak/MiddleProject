namespace DataProcessorService.Api.Endpoints;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Application.Contracts;
using DataProcessorService.Application.Readings.GetLatestReadings;
using DataProcessorService.Application.Readings.GetReadingAggregates;
using DataProcessorService.Application.Readings.GetReadings;
using DataProcessorService.Application.Sensors.GetSensors;
using DataProcessorService.Domain.Enums;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// The read side of the service: informational endpoints for developers and operators, not a
/// consumer-facing API. The dashboard reads through the GraphQL gateway, which queries the database
/// directly, so these are deliberately plain --- ordinary page numbers, sensible defaults, and
/// enums bound the way ASP.NET Core binds them out of the box.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ReadingEndpoints
{
    /// <summary>Maps every read endpoint.</summary>
    /// <param name="app">The application to map onto.</param>
    /// <returns>The application, for chaining.</returns>
    public static WebApplication MapReadingEndpoints(this WebApplication app)
    {
        var readings = app.MapGroup("/api/readings").WithTags("Readings");

        readings
            .MapGet(
                "/",
                async (
                    ISender sender,
                    CancellationToken cancellationToken,
                    string? location = null,
                    SensorType? sensorType = null,
                    DateTimeOffset? from = null,
                    DateTimeOffset? to = null,
                    int page = 1,
                    int pageSize = GetReadingsQuery.DefaultPageSize
                ) =>
                    (
                        await sender.SendAsync(
                            new GetReadingsQuery(
                                location,
                                sensorType,
                                from,
                                to,
                                page,
                                pageSize
                            ),
                            cancellationToken
                        )
                    ).ToHttpResult()
            )
            .WithName("GetReadings")
            .WithSummary("Lists readings, newest first.")
            .WithDescription(
                "Ordered by collection time descending, then by id so that readings sharing a "
                    + "collection instant page deterministically. The response carries totalCount "
                    + "and totalPages alongside the items."
            )
            .Produces<PagedResult<ReadingDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        readings
            .MapGet(
                "/latest",
                async (ISender sender, CancellationToken cancellationToken) =>
                    (
                        await sender.SendAsync(new GetLatestReadingsQuery(), cancellationToken)
                    ).ToHttpResult()
            )
            .WithName("GetLatestReadings")
            .WithSummary("Returns the most recent reading for every sensor.")
            .Produces<IReadOnlyList<ReadingDto>>();

        readings
            .MapGet(
                "/aggregate",
                async (
                    ISender sender,
                    ReadingMetric metric,
                    CancellationToken cancellationToken,
                    AggregationInterval interval = AggregationInterval.Hour,
                    DateTimeOffset? from = null,
                    DateTimeOffset? to = null,
                    string? location = null
                ) =>
                    (
                        await sender.SendAsync(
                            new GetReadingAggregatesQuery(metric, interval, from, to, location),
                            cancellationToken
                        )
                    ).ToHttpResult()
            )
            .WithName("GetReadingAggregates")
            .WithSummary("Aggregates one metric into time periods, grouped by location.")
            .WithDescription(
                "Each row covers one interval at one location, reporting count, average, minimum "
                    + "and maximum. Periods are aligned to UTC boundaries, so an hourly period "
                    + "starts exactly on the hour. Only metric is required: interval defaults to "
                    + "Hour and the range defaults to a window suited to the interval, ending now. "
                    + "For MotionDetected the average is the fraction of readings with motion. "
                    + "The range is capped at 90 days and 2000 periods."
            )
            .Produces<IReadOnlyList<AggregatePeriodDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet(
                "/api/sensors",
                async (ISender sender, CancellationToken cancellationToken) =>
                    (await sender.SendAsync(new GetSensorsQuery(), cancellationToken)).ToHttpResult()
            )
            .WithTags("Sensors")
            .WithName("GetSensors")
            .WithSummary("Lists the sensor catalogue.")
            .WithDescription(
                "Sensors are created on first sight during ingestion, so this reflects what has "
                    + "actually been observed rather than a configured inventory."
            )
            .Produces<IReadOnlyList<SensorDto>>();

        return app;
    }
}
