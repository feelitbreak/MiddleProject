namespace GraphQLGatewayService.Api.GraphQL.Queries;

using GraphQLGatewayService.Domain.Contracts;
using GraphQLGatewayService.Infrastructure.Persistence;
using GraphQLGatewayService.Infrastructure.Persistence.Queries;
using GreenDonut.Data;
using HotChocolate.Types.Pagination;
using Microsoft.EntityFrameworkCore;

/// <summary>Reading queries: the paged feed and the latest value per sensor.</summary>
[QueryType]
internal static partial class ReadingGraphQLQueries
{
    /// <summary>
    /// Readings matching the filter, newest first. A cursor connection rather than page numbers:
    /// readings arrive continuously, so offsets would make page two repeat rows from page one.
    /// </summary>
    [UseConnection(IncludeTotalCount = true)]
    public static async Task<PageConnection<Reading>> GetReadingsAsync(
        ReadingFilter? where,
        PagingArguments pagingArguments,
        MeterReadingsDbContext context,
        CancellationToken cancellationToken
    ) =>
        await context
            .FilterReadings(where)
            .ProjectToReading()
            // Ordered on the projection so keyset paging can read these keys. Id breaks the
            // tie: a whole poll batch shares one CollectedAt.
            .OrderByDescending(reading => reading.CollectedAt)
            .ThenByDescending(reading => reading.Id)
            .ToPageAsync(pagingArguments, cancellationToken);

    /// <summary>
    /// The most recent reading from each sensor matching the filter, for a latest-values display.
    /// </summary>
    public static async Task<IReadOnlyList<Reading>> GetLatestReadingsAsync(
        ReadingFilter? where,
        MeterReadingsDbContext context,
        CancellationToken cancellationToken
    ) =>
        await context
            .LatestPerSensor(where)
            .ProjectToReading()
            .OrderBy(reading => reading.Sensor.Name)
            .ThenBy(reading => reading.Sensor.Type)
            .ToListAsync(cancellationToken);
}
