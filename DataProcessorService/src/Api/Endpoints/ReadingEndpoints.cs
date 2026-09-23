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
/// Query parameters for listing readings, bound as one object.
/// <para>
/// Grouped rather than declared as eight separate lambda parameters, which trips S107. The
/// grouping is the better shape anyway: the filter travels as a unit, and Swagger documents each
/// property individually all the same.
/// </para>
/// </summary>
public sealed class GetReadingsRequest
{
    /// <summary>Gets or sets the location filter, or null for every location.</summary>
    public string? Location { get; set; }

    /// <summary>Gets or sets the sensor type filter, or null for every type.</summary>
    public SensorType? SensorType { get; set; }

    /// <summary>Gets or sets the inclusive lower bound on collection time.</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>Gets or sets the exclusive upper bound on collection time.</summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>Gets or sets the one-based page number. Defaults to the first page.</summary>
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    /// <summary>Converts the bound request into the query to dispatch.</summary>
    public GetReadingsQuery ToQuery() =>
        new(
            this.Location,
            this.SensorType,
            this.From,
            this.To,
            this.Page ?? 1,
            this.PageSize ?? GetReadingsQuery.DefaultPageSize
        );
}

/// <summary>
/// The read side of the service: informational endpoints for developers and operators, not a
/// consumer-facing API. The dashboard reads through the GraphQL gateway, which queries the database
/// directly, so these are deliberately plain --- ordinary page numbers, sensible defaults, and
/// enums bound the way ASP.NET Core binds them out of the box.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Endpoint wiring only; the behaviour lives in the handlers those endpoints dispatch to.")]
public static class ReadingEndpoints
{
    /// <summary>Maps every read endpoint.</summary>
    public static WebApplication MapReadingEndpoints(this WebApplication app)
    {
        var readings = app.MapGroup("/api/readings").WithTags("Readings").RequireAuthorization();

        readings
            .MapGet(
                "/",
                async (
                    [AsParameters] GetReadingsRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                    (
                        await sender.SendAsync(request.ToQuery(), cancellationToken)
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
            // Mapped on the app rather than the readings group, so it needs its own guard.
            .RequireAuthorization()
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
