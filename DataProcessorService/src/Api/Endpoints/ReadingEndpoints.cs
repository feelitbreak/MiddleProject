namespace DataProcessorService.Api.Endpoints;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Application.Contracts;
using DataProcessorService.Application.Readings.GetLatestReadings;
using DataProcessorService.Application.Readings.GetReadingAggregates;
using DataProcessorService.Application.Readings.GetReadings;
using DataProcessorService.Application.Sensors.GetSensors;
using DataProcessorService.Domain.Common;
using DataProcessorService.Domain.Enums;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// The read side of the service, exposed as minimal API endpoints that bind query parameters,
/// dispatch a query, and translate the result.
/// <para>
/// Enum-valued parameters are bound as strings and converted through
/// <see cref="EnumVocabulary{TEnum}"/> rather than being typed as enums directly. Minimal API
/// parameter binding does not consult the JSON serializer, so a declared enum parameter would
/// require the C# member name (<c>AirQuality</c>) while responses emit the canonical spelling
/// (<c>air_quality</c>) --- two vocabularies in one API. Converting here also turns an unrecognised
/// value into a problem response that lists what was expected, instead of an unhandled binding
/// exception.
/// </para>
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
                    string? sensorType = null,
                    DateTimeOffset? from = null,
                    DateTimeOffset? to = null,
                    int limit = GetReadingsQuery.DefaultLimit,
                    string? cursor = null
                ) =>
                {
                    SensorType? parsedType = null;

                    if (sensorType is not null)
                    {
                        if (!EnumVocabulary<SensorType>.TryParse(sensorType, out var parsed))
                        {
                            return ApiResults.ValidationProblem(
                                $"sensorType must be one of: {EnumVocabulary<SensorType>.AcceptedValues}."
                            );
                        }

                        parsedType = parsed;
                    }

                    var result = await sender.SendAsync(
                        new GetReadingsQuery(location, parsedType, from, to, limit, cursor),
                        cancellationToken
                    );

                    return result.ToHttpResult();
                }
            )
            .WithName("GetReadings")
            .WithSummary("Lists readings newest first.")
            .WithDescription(
                "Paged by opaque cursor rather than page number: readings arrive continuously at "
                    + "the head of the ordering, so a numbered page would shift between requests. "
                    + "Pass the nextCursor from a response to fetch the following page. "
                    + "sensorType accepts air_quality, energy or motion."
            )
            .Produces<CursorPage<ReadingDto>>()
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
                    string metric,
                    string bucket,
                    DateTimeOffset from,
                    DateTimeOffset to,
                    CancellationToken cancellationToken,
                    string? location = null
                ) =>
                {
                    if (!EnumVocabulary<ReadingMetric>.TryParse(metric, out var parsedMetric))
                    {
                        return ApiResults.ValidationProblem(
                            $"metric must be one of: {EnumVocabulary<ReadingMetric>.AcceptedValues}."
                        );
                    }

                    if (!EnumVocabulary<BucketSize>.TryParse(bucket, out var parsedBucket))
                    {
                        return ApiResults.ValidationProblem(
                            $"bucket must be one of: {EnumVocabulary<BucketSize>.AcceptedValues}."
                        );
                    }

                    var result = await sender.SendAsync(
                        new GetReadingAggregatesQuery(
                            parsedMetric,
                            parsedBucket,
                            from,
                            to,
                            location
                        ),
                        cancellationToken
                    );

                    return result.ToHttpResult();
                }
            )
            .WithName("GetReadingAggregates")
            .WithSummary("Aggregates one metric into time buckets, grouped by location.")
            .WithDescription(
                "metric accepts co2, pm25, humidity, motion_detected or energy_kwh; bucket accepts "
                    + "hour, day, week or month. Buckets are aligned to UTC. The time range is "
                    + "mandatory and capped, both in span and in the number of buckets it may "
                    + "produce, so that a single request cannot ask for an unbounded scan. "
                    + "For motion_detected the average is the fraction of readings with motion."
            )
            .Produces<IReadOnlyList<AggregateBucketDto>>()
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
