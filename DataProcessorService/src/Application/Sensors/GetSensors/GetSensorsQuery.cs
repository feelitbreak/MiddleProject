namespace DataProcessorService.Application.Sensors.GetSensors;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Contracts;
using DataProcessorService.Domain.Common;

/// <summary>
/// Lists the sensor catalogue, which the dashboard needs to populate its filters. Sensors are
/// created on first sight during ingestion, so this reflects what has actually been observed rather
/// than a configured inventory.
/// </summary>
public sealed class GetSensorsQuery : IQuery<IReadOnlyList<SensorDto>>;

/// <summary>Handles <see cref="GetSensorsQuery"/>.</summary>
/// <param name="queries">Read-side access to the sensor catalogue.</param>
public sealed class GetSensorsQueryHandler(ISensorQueries queries)
    : IQueryHandler<GetSensorsQuery, IReadOnlyList<SensorDto>>
{
    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<SensorDto>>> HandleAsync(
        GetSensorsQuery query,
        CancellationToken cancellationToken
    ) => Result.Success(await queries.ListAsync(cancellationToken));
}
