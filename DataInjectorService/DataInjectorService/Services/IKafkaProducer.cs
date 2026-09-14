namespace DataInjectorService.Services;

using DataInjectorService.Models;

/// <summary>Publishes meter readings to Kafka.</summary>
public interface IKafkaProducer : IAsyncDisposable
{
    /// <summary>
    /// Publishes one reading, keyed so that a sensor's readings share a partition and stay ordered.
    /// </summary>
    Task ProduceAsync(MeterReading reading, CancellationToken cancellationToken);
}
