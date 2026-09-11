namespace DataProcessorService.Application.Readings.GetReadings;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Contracts;
using DataProcessorService.Domain.Common;
using DataProcessorService.Domain.Enums;

/// <summary>Lists readings newest-first, filtered and paged.</summary>
/// <param name="location">Restrict to one location, or null for every location.</param>
/// <param name="sensorType">Restrict to one sensor type, or null for every type.</param>
/// <param name="from">Inclusive lower bound on collection time.</param>
/// <param name="to">Exclusive upper bound on collection time.</param>
/// <param name="page">One-based page number.</param>
/// <param name="pageSize">How many readings per page.</param>
public sealed class GetReadingsQuery(
    string? location,
    SensorType? sensorType,
    DateTimeOffset? from,
    DateTimeOffset? to,
    int page,
    int pageSize
) : IQuery<PagedResult<ReadingDto>>
{
    /// <summary>The largest page a caller may request.</summary>
    public const int MaxPageSize = 500;

    /// <summary>The page size used when a caller does not specify one.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>Gets the location filter.</summary>
    public string? Location { get; } = location;

    /// <summary>Gets the sensor type filter.</summary>
    public SensorType? SensorType { get; } = sensorType;

    /// <summary>Gets the inclusive lower bound on collection time.</summary>
    public DateTimeOffset? From { get; } = from;

    /// <summary>Gets the exclusive upper bound on collection time.</summary>
    public DateTimeOffset? To { get; } = to;

    /// <summary>Gets the one-based page number.</summary>
    public int Page { get; } = page;

    /// <summary>Gets the page size.</summary>
    public int PageSize { get; } = pageSize;
}

/// <summary>Validates the request and returns the requested page.</summary>
/// <param name="queries">Read-side access to stored readings.</param>
public sealed class GetReadingsQueryHandler(IReadingQueries queries)
    : IQueryHandler<GetReadingsQuery, PagedResult<ReadingDto>>
{
    /// <inheritdoc/>
    public async Task<Result<PagedResult<ReadingDto>>> HandleAsync(
        GetReadingsQuery query,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Page < 1)
        {
            return Failure("page must be 1 or greater.");
        }

        if (query.PageSize is < 1 or > GetReadingsQuery.MaxPageSize)
        {
            return Failure($"pageSize must be between 1 and {GetReadingsQuery.MaxPageSize}.");
        }

        if (query.From is not null && query.To is not null && query.From >= query.To)
        {
            return Failure("from must be earlier than to.");
        }

        var filter = new ReadingFilter(
            NormalizeLocation(query.Location),
            query.SensorType,
            Normalize(query.From),
            Normalize(query.To)
        );

        var totalCount = await queries.CountAsync(filter, cancellationToken);

        var readings = await queries.ListAsync(
            filter,
            (query.Page - 1) * query.PageSize,
            query.PageSize,
            cancellationToken
        );

        return Result.Success(
            new PagedResult<ReadingDto>(readings, query.Page, query.PageSize, totalCount)
        );
    }

    private static Result<PagedResult<ReadingDto>> Failure(string description) =>
        Result.Failure<PagedResult<ReadingDto>>(Error.CreateValidation(description));

    private static DateTimeOffset? Normalize(DateTimeOffset? value) =>
        value is null ? null : UtcInstant.Normalize(value.Value);

    private static string? NormalizeLocation(string? location) =>
        string.IsNullOrWhiteSpace(location) ? null : location.Trim();
}
