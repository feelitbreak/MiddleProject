namespace DataProcessorService.Application.Abstractions.Persistence;

using DataProcessorService.Domain.Enums;

/// <summary>
/// Resolves a sensor's natural key (location plus kind) to its surrogate key, creating the sensor
/// on first sight.
/// </summary>
public interface ISensorRegistry
{
    /// <summary>
    /// Returns the surrogate key for the given sensor, inserting it if it is not yet known.
    /// </summary>
    /// <param name="name">The location the sensor reports from.</param>
    /// <param name="type">The kind of data the sensor emits.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The sensor's surrogate key.</returns>
    Task<int> GetOrCreateIdAsync(string name, SensorType type, CancellationToken cancellationToken);
}
