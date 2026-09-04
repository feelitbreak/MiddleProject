namespace DataProcessorService.Infrastructure.Messaging;

using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Logging extensions for <see cref="KafkaConsumerService"/>, checking
/// <see cref="ILogger.IsEnabled"/> before formatting so that CA1873 is satisfied without
/// source-generated logging --- the same approach the sibling DataInjectorService uses.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class KafkaConsumerServiceLog
{
    /// <summary>Logs that the consumer subscribed to its topic.</summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="topic">The subscribed topic.</param>
    /// <param name="groupId">The consumer group.</param>
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
    /// <param name="logger">Logger instance.</param>
    internal static void ConsumerStopped(this ILogger<KafkaConsumerService> logger)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Kafka consumer stopped");
        }
    }

    /// <summary>Logs a broker-reported error.</summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="code">The librdkafka error code.</param>
    /// <param name="reason">The error reason.</param>
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
    /// <param name="logger">Logger instance.</param>
    /// <param name="count">How many partitions were assigned.</param>
    internal static void PartitionsAssigned(this ILogger<KafkaConsumerService> logger, int count)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Assigned {PartitionCount} partition(s)", count);
        }
    }

    /// <summary>Logs a partition revocation.</summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="count">How many partitions were revoked.</param>
    internal static void PartitionsRevoked(this ILogger<KafkaConsumerService> logger, int count)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Revoked {PartitionCount} partition(s)", count);
        }
    }

    /// <summary>Logs a failed poll.</summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="exception">The exception thrown.</param>
    /// <param name="reason">The error reason.</param>
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
    /// <param name="logger">Logger instance.</param>
    /// <param name="exception">The exception thrown.</param>
    /// <param name="reason">The error reason.</param>
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
    /// <param name="logger">Logger instance.</param>
    /// <param name="inserted">How many rows were written.</param>
    /// <param name="duplicatesSkipped">How many rows were skipped as duplicates.</param>
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

    /// <summary>Logs a transient batch failure that will be retried.</summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="exception">The exception thrown.</param>
    /// <param name="attempt">The attempt that just failed.</param>
    /// <param name="maxAttempts">The configured attempt limit.</param>
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
    /// <param name="logger">Logger instance.</param>
    /// <param name="exception">The exception thrown.</param>
    /// <param name="attempts">How many attempts were made.</param>
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
    /// <param name="logger">Logger instance.</param>
    /// <param name="exception">The exception thrown.</param>
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

/// <summary>Logging extensions for <see cref="DeadLetterProducer"/>.</summary>
[ExcludeFromCodeCoverage]
internal static class DeadLetterProducerLog
{
    /// <summary>Logs a message moved to the dead-letter topic.</summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="topic">The topic the message came from.</param>
    /// <param name="partition">The partition the message came from.</param>
    /// <param name="offset">The offset the message came from.</param>
    /// <param name="reason">The short rejection reason.</param>
    /// <param name="detail">The human-readable rejection detail.</param>
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
