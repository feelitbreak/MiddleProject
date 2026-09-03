namespace DataInjectorService.Services;

using DataInjectorService.Models;

/// <summary>
/// Abstracts Kafka message production so the polling service can be tested without a real broker.
/// </summary>
public interface IKafkaProducer : IAsyncDisposable
{
    /// <summary>
    /// Publishes a single <see cref="MeterReading"/> to the configured Kafka topic.
    /// The message key is <c>{Type}:{Name}</c> to ensure readings from the same sensor
    /// land on the same partition (preserving order per sensor).
    /// </summary>
    /// <param name="reading">The meter reading to publish.</param>
    /// <param name="cancellationToken">Propagates cancellation from the caller.</param>
    Task ProduceAsync(MeterReading reading, CancellationToken cancellationToken);
}
