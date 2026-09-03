namespace DataInjectorService.Services;

/// <summary>
/// Log helper methods for <see cref="KafkaProducer"/>.
/// Each method guards with <see cref="ILogger.IsEnabled"/> before constructing any arguments,
/// eliminating the CA1873 "possibly unnecessary argument evaluation" warning without requiring
/// source generation or partial methods.
/// </summary>
internal static class KafkaProducerLog
{
    internal static void ProducerInitialised(
        this ILogger<KafkaProducer> logger,
        string servers,
        string topic
    )
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Kafka producer initialised. Bootstrap: {Servers}, Topic: {Topic}",
                servers,
                topic
            );
        }
    }

    internal static void ReadingPublished(
        this ILogger<KafkaProducer> logger,
        string topic,
        int partition,
        long offset,
        string key
    )
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Published reading to {Topic} [{Partition}@{Offset}] key={Key}",
                topic,
                partition,
                offset,
                key
            );
        }
    }
}
