namespace DataProcessorService.Application.Readings.GetReadings;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Contracts;
using DataProcessorService.Domain.Common;
using DataProcessorService.Domain.Enums;

/// <summary>Lists readings newest-first, filtered and paged.</summary>
public sealed class GetReadingsQuery(
    string? location,
    SensorType? sensorType,
    DateTimeOffset? from,
    DateTimeOffset? to,
    int page,
    int pageSize
) : IQuery<PagedResult<ReadingDto>>
{
    public const int MaxPageSize = 500;

    public const int DefaultPageSize = 50;

    public string? Location { get; } = location;

    public SensorType? SensorType { get; } = sensorType;

    /// <summary>Gets the inclusive lower bound on collection time.</summary>
    public DateTimeOffset? From { get; } = from;

    /// <summary>Gets the exclusive upper bound on collection time.</summary>
    public DateTimeOffset? To { get; } = to;

    /// <summary>Gets the one-based page number.</summary>
    public int Page { get; } = page;

    public int PageSize { get; } = pageSize;
}

/// <summary>Validates the request and returns the requested page.</summary>
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
