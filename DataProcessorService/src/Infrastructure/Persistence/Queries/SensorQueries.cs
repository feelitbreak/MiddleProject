namespace DataProcessorService.Infrastructure.Persistence.Queries;

using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Contracts;
using Microsoft.EntityFrameworkCore;

/// <summary>Read-side queries over the sensor catalogue.</summary>
public sealed class SensorQueries(MeterReadingsDbContext context) : ISensorQueries
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<SensorDto>> ListAsync(CancellationToken cancellationToken) =>
        await context
            .Sensors.AsNoTracking()
            .OrderBy(sensor => sensor.Name)
            .ThenBy(sensor => sensor.Type)
            .Select(sensor => new SensorDto(sensor.Id, sensor.Name, sensor.Type))
            .ToListAsync(cancellationToken);
}
