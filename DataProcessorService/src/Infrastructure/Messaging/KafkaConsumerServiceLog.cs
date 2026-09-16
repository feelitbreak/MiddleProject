namespace DataProcessorService.Infrastructure.Messaging;

using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Logging extensions for <see cref="KafkaConsumerService"/>, checking
/// <see cref="ILogger.IsEnabled"/> before formatting so that CA1873 is satisfied without
/// source-generated logging --- the same approach the sibling DataInjectorService uses.
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

    /// <summary>Logs a failure to resume paused partitions.</summary>
    internal static void ResumeFailed(
        this ILogger<KafkaConsumerService> logger,
        Exception exception,
        string reason
    )
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                exception,
                "Resuming paused partitions failed, most likely because the assignment was "
                    + "revoked while backing off: {Reason}",
                reason
            );
        }
    }

    /// <summary>Logs a successfully persisted batch.</summary>
    internal static void BatchPersisted(
        this ILogger<KafkaConsumerService> logger,
        int inserted,
        int duplicatesSkipped
    )
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Persisted batch: {Inserted} inserted, {DuplicatesSkipped} duplicate(s) skipped",
                inserted,
                duplicatesSkipped
            );
        }
    }

    /// <summary>Logs a failure to announce a batch that was nevertheless stored.</summary>
    internal static void ReadingsPersistedPublishFailed(
        this ILogger<KafkaConsumerService> logger,
        Exception exception,
        string reason
    )
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                exception,
                "Publishing the readings-persisted event failed; the batch is stored and the next "
                    + "one will announce it: {Reason}",
                reason
            );
        }
    }

    /// <summary>Logs a transient batch failure that will be retried.</summary>
    internal static void BatchRetrying(
        this ILogger<KafkaConsumerService> logger,
        Exception exception,
        int attempt,
        int maxAttempts
    )
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                exception,
                "Persisting a batch failed transiently on attempt {Attempt} of {MaxAttempts}; "
                    + "backing off before retrying",
                attempt,
                maxAttempts
            );
        }
    }

    /// <summary>Logs a batch abandoned after exhausting its attempts.</summary>
    internal static void BatchAbandoned(
        this ILogger<KafkaConsumerService> logger,
        Exception exception,
        int attempts
    )
    {
        if (logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(
                exception,
                "Persisting a batch still failed after {Attempts} attempt(s); dead-lettering it "
                    + "so the partition is not blocked",
                attempts
            );
        }
    }

    /// <summary>Logs a batch failure that retrying cannot fix.</summary>
    internal static void BatchFailedPermanently(
        this ILogger<KafkaConsumerService> logger,
        Exception exception
    )
    {
        if (logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(
                exception,
                "Persisting a batch failed for a reason that retrying cannot fix; dead-lettering it"
            );
        }
    }
}

/// <summary>Logging extensions for <see cref="ReadingsPersistedProducer"/>.</summary>
[ExcludeFromCodeCoverage(Justification = "Logging message definitions: no branching behaviour to cover.")]
internal static class ReadingsPersistedProducerLog
{
    /// <summary>Logs an announced batch.</summary>
    internal static void ReadingsPersisted(
        this ILogger<ReadingsPersistedProducer> logger,
        string topic,
        int readingCount,
        int sensorCount
    )
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Announced {ReadingCount} reading(s) across {SensorCount} sensor(s) on {Topic}",
                readingCount,
                sensorCount,
                topic
            );
        }
    }
}

/// <summary>Logging extensions for <see cref="DeadLetterProducer"/>.</summary>
[ExcludeFromCodeCoverage(Justification = "Logging message definitions: no branching behaviour to cover.")]
internal static class DeadLetterProducerLog
{
    /// <summary>Logs a message moved to the dead-letter topic.</summary>
    internal static void MessageDeadLettered(
        this ILogger<DeadLetterProducer> logger,
        string topic,
        int partition,
        long offset,
        string reason,
        string detail
    )
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                "Dead-lettered {Topic}[{Partition}]@{Offset} ({Reason}): {Detail}",
                topic,
                partition,
                offset,
                reason,
                detail
            );
        }
    }
}
