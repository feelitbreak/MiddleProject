namespace DataProcessorService.Application.Abstractions.Persistence;

using DataProcessorService.Domain.Enums;

/// <summary>
/// Resolves a sensor's natural key --- location plus kind, since the upstream API supplies no
/// identifier --- to its surrogate key, creating the sensor on first sight.
/// </summary>
public interface ISensorRegistry
{
    Task<int> GetOrCreateIdAsync(string name, SensorType type, CancellationToken cancellationToken);
}
