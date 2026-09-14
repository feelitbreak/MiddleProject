namespace DataProcessorService.Application.Readings.GetLatestReadings;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Contracts;
using DataProcessorService.Domain.Common;

/// <summary>
/// Returns the most recent reading for every sensor. This is the dashboard's "current state" view.
/// </summary>
public sealed class GetLatestReadingsQuery : IQuery<IReadOnlyList<ReadingDto>>;

/// <summary>Handles <see cref="GetLatestReadingsQuery"/>.</summary>
public sealed class GetLatestReadingsQueryHandler(IReadingQueries queries)
    : IQueryHandler<GetLatestReadingsQuery, IReadOnlyList<ReadingDto>>
{
    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<ReadingDto>>> HandleAsync(
        GetLatestReadingsQuery query,
        CancellationToken cancellationToken
    ) => Result.Success(await queries.ListLatestPerSensorAsync(cancellationToken));
}
