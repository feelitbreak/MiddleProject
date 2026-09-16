namespace NotificationService.Messaging;

using Confluent.Kafka;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Logging extensions for <see cref="KafkaConsumerService"/>, checking
/// <see cref="ILogger.IsEnabled"/> before formatting so that CA1873 is satisfied without
/// source-generated logging — the same approach the sibling services use.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Logging message definitions: no branching behaviour to cover.")]
internal static class KafkaConsumerServiceLog
{
    /// <summary>Logs that the consumer subscribed to its topic.</summary>
    internal static void ConsumerStarted(
        this ILogger<KafkaConsumerService> logger,
        string topic,
        string groupId
    )
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Kafka consumer started. Topic: {Topic}, Group: {GroupId}",
                topic,
                groupId
            );
        }
    }

    /// <summary>Logs that the consumer left its group.</summary>
    internal static void ConsumerStopped(this ILogger<KafkaConsumerService> logger)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Kafka consumer stopped");
        }
    }

    /// <summary>Logs a broker-reported error.</summary>
    internal static void ConsumerError(
        this ILogger<KafkaConsumerService> logger,
        ErrorCode code,
        string reason
    )
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning("Kafka consumer error {Code}: {Reason}", code, reason);
        }
    }

    /// <summary>Logs a partition assignment.</summary>
    internal static void PartitionsAssigned(this ILogger<KafkaConsumerService> logger, int count)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Assigned {PartitionCount} partition(s)", count);
        }
    }

    /// <summary>Logs a partition revocation.</summary>
    internal static void PartitionsRevoked(this ILogger<KafkaConsumerService> logger, int count)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Revoked {PartitionCount} partition(s)", count);
        }
    }

    /// <summary>Logs a failed poll.</summary>
    internal static void ConsumeFailed(
        this ILogger<KafkaConsumerService> logger,
        Exception exception,
        string reason
    )
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(exception, "Consuming from Kafka failed: {Reason}", reason);
        }
    }

    /// <summary>Logs a failed offset commit.</summary>
    internal static void CommitFailed(
        this ILogger<KafkaConsumerService> logger,
        Exception exception,
        string reason
    )
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                exception,
                "Committing the offset failed; the event was broadcast and may be redelivered: {Reason}",
                reason
            );
        }
    }

    /// <summary>Logs a message that could not be decoded and was therefore not broadcast.</summary>
    internal static void MessageDropped(
        this ILogger<KafkaConsumerService> logger,
        string topic,
        int partition,
        long offset,
        string reason
    )
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                "Dropped {Topic}[{Partition}]@{Offset}: {Reason}",
                topic,
                partition,
                offset,
                reason
            );
        }
    }

    /// <summary>Logs an event broadcast to connected clients.</summary>
    internal static void EventBroadcast(
        this ILogger<KafkaConsumerService> logger,
        int readingCount,
        int sensorCount
    )
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Broadcast a change covering {ReadingCount} reading(s) across {SensorCount} sensor(s)",
                readingCount,
                sensorCount
            );
        }
    }
}
