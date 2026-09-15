namespace GraphQLGatewayService.Api.GraphQL.Queries;

using GraphQLGatewayService.Domain.Contracts;
using GraphQLGatewayService.Infrastructure.Persistence;
using GraphQLGatewayService.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

/// <summary>Catalogue queries that populate a dashboard's filter controls.</summary>
[QueryType]
internal static partial class SensorGraphQLQueries
{
    /// <summary>
    /// The sensor catalogue. Sensors are created on first sight during ingestion, so this is what
    /// has been observed rather than a configured inventory.
    /// </summary>
    public static async Task<IReadOnlyList<Sensor>> GetSensorsAsync(
        MeterReadingsDbContext context,
        CancellationToken cancellationToken
    ) =>
        await context
            .Sensors.OrderBy(sensor => sensor.Name)
            .ThenBy(sensor => sensor.Type)
            .Select(sensor => new Sensor
            {
                Id = sensor.Id,
                Name = sensor.Name,
                Type = sensor.Type,
            })
            .ToListAsync(cancellationToken);

    /// <summary>Every distinct location, ordered, for a location filter.</summary>
    public static async Task<IReadOnlyList<string>> GetLocationsAsync(
        MeterReadingsDbContext context,
        CancellationToken cancellationToken
    ) => await context.Locations().ToListAsync(cancellationToken);
}
