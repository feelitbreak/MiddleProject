namespace DataProcessorService.Application.Readings.GetReadings;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Contracts;
using DataProcessorService.Domain.Common;
using DataProcessorService.Domain.Enums;

/// <summary>Lists readings newest-first, filtered and paged by cursor.</summary>
/// <param name="location">Restrict to one location, or null for every location.</param>
/// <param name="sensorType">Restrict to one sensor type, or null for every type.</param>
/// <param name="from">Inclusive lower bound on collection time.</param>
/// <param name="to">Exclusive upper bound on collection time.</param>
/// <param name="limit">Maximum readings to return.</param>
/// <param name="cursor">Opaque position to resume from, from a previous response.</param>
public sealed class GetReadingsQuery(
    string? location,
    SensorType? sensorType,
    DateTimeOffset? from,
    DateTimeOffset? to,
    int limit,
    string? cursor
) : IQuery<CursorPage<ReadingDto>>
{
    /// <summary>The largest page a caller may request.</summary>
    public const int MaxLimit = 500;

    /// <summary>The page size used when a caller does not specify one.</summary>
    public const int DefaultLimit = 100;

    /// <summary>Gets the location filter.</summary>
    public string? Location { get; } = location;

    /// <summary>Gets the sensor type filter.</summary>
    public SensorType? SensorType { get; } = sensorType;

    /// <summary>Gets the inclusive lower bound on collection time.</summary>
    public DateTimeOffset? From { get; } = from;

    /// <summary>Gets the exclusive upper bound on collection time.</summary>
    public DateTimeOffset? To { get; } = to;

    /// <summary>Gets the requested page size.</summary>
    public int Limit { get; } = limit;

    /// <summary>Gets the opaque cursor to resume from.</summary>
    public string? Cursor { get; } = cursor;
}

/// <summary>Validates the request, resolves the cursor, and shapes the page.</summary>
/// <param name="queries">Read-side access to stored readings.</param>
public sealed class GetReadingsQueryHandler(IReadingQueries queries)
    : IQueryHandler<GetReadingsQuery, CursorPage<ReadingDto>>
{
    /// <inheritdoc/>
    public async Task<Result<CursorPage<ReadingDto>>> HandleAsync(
        GetReadingsQuery query,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Limit is < 1 or > GetReadingsQuery.MaxLimit)
        {
            return Result.Failure<CursorPage<ReadingDto>>(
                Error.CreateValidation(
                    $"limit must be between 1 and {GetReadingsQuery.MaxLimit}."
                )
            );
        }

        if (query.From is not null && query.To is not null && query.From >= query.To)
        {
            return Result.Failure<CursorPage<ReadingDto>>(
                Error.CreateValidation("from must be earlier than to.")
            );
        }

        ReadingCursor? cursor = null;

        if (query.Cursor is not null && !ReadingCursor.TryDecode(query.Cursor, out cursor))
        {
            return Result.Failure<CursorPage<ReadingDto>>(
                Error.CreateValidation("cursor is not a valid pagination token.")
            );
        }

        var filter = new ReadingFilter(
            NormalizeLocation(query.Location),
            query.SensorType,
            Normalize(query.From),
            Normalize(query.To)
        );

        // One row beyond the page is fetched so that "is there more" costs nothing extra; a
        // COUNT(*) over the same filter would double the work for a question the extra row
        // already answers.
        var readings = await queries.ListAsync(filter, query.Limit, cursor, cancellationToken);

        var hasMore = readings.Count > query.Limit;
        var items = hasMore ? readings.Take(query.Limit).ToList() : readings;

        var nextCursor = hasMore
            ? new ReadingCursor(items[^1].CollectedAt, items[^1].Id).Encode()
            : null;

        return Result.Success(new CursorPage<ReadingDto>(items, nextCursor));
    }

    private static DateTimeOffset? Normalize(DateTimeOffset? value) =>
        value is null ? null : UtcInstant.Normalize(value.Value);

    private static string? NormalizeLocation(string? location) =>
        string.IsNullOrWhiteSpace(location) ? null : location.Trim();
}
