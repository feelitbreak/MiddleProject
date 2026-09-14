namespace DataProcessorService.Infrastructure.Messaging;

using Confluent.Kafka;
using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Application.Readings.IngestReadingBatch;
using DataProcessorService.Domain.Common;
using DataProcessorService.Infrastructure.Configuration;
using DataProcessorService.Infrastructure.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

/// <summary>
/// Consumes the readings topic in batches and persists each batch atomically.
/// <para>
/// Ordering within a batch is decode, then dead-letter the undecodable, then persist, then commit
/// offsets. A crash in the window between the database commit and the offset commit redelivers the
/// batch, which the idempotent ingestion statement turns into a no-op --- that window is precisely
/// why conflict-skipping insertion is load-bearing rather than a nicety.
/// </para>
/// </summary>
public sealed class KafkaConsumerService(
    IServiceScopeFactory scopeFactory,
    IDeadLetterProducer deadLetterProducer,
    ConsumerHeartbeat heartbeat,
    DataProcessorMetrics metrics,
    IOptions<KafkaOptions> options,
    ILogger<KafkaConsumerService> logger
) : BackgroundService
{
    /// <summary>
    /// How long a single poll blocks. Short on purpose: the loop interleaves asynchronous database
    /// work between polls, and a paused consumer must still be polled regularly to hold its
    /// partition assignment.
    /// </summary>
    private static readonly TimeSpan PollTimeout = TimeSpan.FromMilliseconds(100);

    private readonly KafkaOptions options = options.Value;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Must be the very first statement. StartAsync only returns once ExecuteAsync hits its
        // first *incomplete* await, so without this the subscribe-and-poll below would run
        // synchronously on the host start-up path and the web host would never bind its port.
        await Task.Yield();

        using var consumer = this.BuildConsumer();
        consumer.Subscribe(this.options.MeterReadingsTopic);
        logger.ConsumerStarted(this.options.MeterReadingsTopic, this.options.ConsumerGroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var batch = this.CollectBatch(consumer, stoppingToken);

                heartbeat.Beat();
                heartbeat.SetAssignment(consumer.Assignment.Count > 0);

                if (batch.Count == 0)
                {
                    continue;
                }

                await this.ProcessBatchAsync(consumer, batch, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during graceful shutdown.
        }
        finally
        {
            // Leaves the group cleanly so the broker reassigns partitions immediately instead of
            // waiting for the session to time out.
            consumer.Close();
            logger.ConsumerStopped();
        }
    }

    private IConsumer<byte[], byte[]> BuildConsumer()
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = this.options.BootstrapServers,
            GroupId = this.options.ConsumerGroupId,

            // Offsets advance only after a batch is durably stored.
            EnableAutoCommit = false,

            // Inert while auto-commit is off, but set explicitly so that re-enabling auto-commit
            // later cannot silently start committing offsets for unprocessed messages.
            EnableAutoOffsetStore = false,

            // Earliest, not Latest: a new consumer group must drain the backlog rather than
            // silently discard everything published before it first connected.
            AutoOffsetReset = AutoOffsetReset.Earliest,
            MaxPollIntervalMs = this.options.MaxPollIntervalMs,
            SessionTimeoutMs = this.options.SessionTimeoutMs,
            PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky,
        };

        return new ConsumerBuilder<byte[], byte[]>(config)
            .SetErrorHandler((_, error) => logger.ConsumerError(error.Code, error.Reason))
            .SetPartitionsAssignedHandler(
                (_, partitions) => logger.PartitionsAssigned(partitions.Count)
            )
            .SetPartitionsRevokedHandler(
                (_, partitions) => logger.PartitionsRevoked(partitions.Count)
            )
            .Build();
    }

    /// <summary>
    /// Accumulates messages until the batch is full, the linger window closes, or shutdown starts.
    /// </summary>
    private List<ConsumeResult<byte[], byte[]>> CollectBatch(
        IConsumer<byte[], byte[]> consumer,
        CancellationToken stoppingToken
    )
    {
        var batch = new List<ConsumeResult<byte[], byte[]>>(this.options.MaxBatchSize);
        var linger = Stopwatch.StartNew();
        var lingerBudget = TimeSpan.FromMilliseconds(this.options.BatchLingerMs);

        while (batch.Count < this.options.MaxBatchSize && !stoppingToken.IsCancellationRequested)
        {
            if (batch.Count > 0 && linger.Elapsed >= lingerBudget)
            {
                break;
            }

            ConsumeResult<byte[], byte[]>? result;

            try
            {
                // The TimeSpan overload, never the CancellationToken one: it returns null
                // cooperatively when nothing is available, which is what lets this loop stay
                // responsive to shutdown and interleave asynchronous work between polls.
                result = consumer.Consume(PollTimeout);
            }
            catch (ConsumeException ex)
            {
                logger.ConsumeFailed(ex, ex.Error.Reason);
                break;
            }

            if (result is null)
            {
                if (batch.Count > 0)
                {
                    break;
                }

                continue;
            }

            if (result.IsPartitionEOF)
            {
                continue;
            }

            batch.Add(result);
        }

        return batch;
    }

    private async Task ProcessBatchAsync(
        IConsumer<byte[], byte[]> consumer,
        List<ConsumeResult<byte[], byte[]>> batch,
        CancellationToken stoppingToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        metrics.MessagesConsumed.Add(batch.Count);

        // Decoding is deterministic, so it happens once rather than on every retry attempt.
        var readings = new List<ReadingToIngest>(batch.Count);

        foreach (var message in batch)
        {
            var decoded = MeterReadingMessageDecoder.Decode(message.Message.Value);

            if (decoded.IsSuccess)
            {
                readings.Add(decoded.Value);
                continue;
            }

            await deadLetterProducer.SendAsync(
                message,
                "decode-failed",
                decoded.Error.Description,
                stoppingToken
            );
        }

        if (readings.Count > 0)
        {
            var persisted = await this.PersistWithRetriesAsync(consumer, readings, stoppingToken);

            if (!persisted)
            {
                await this.DeadLetterBatchAsync(batch, stoppingToken);
            }
            else
            {
                RecordLag(readings);
            }
        }

        CommitOffsets(consumer, batch);
        metrics.BatchesProcessed.Add(1);
        metrics.BatchDuration.Record(stopwatch.Elapsed.TotalSeconds);
    }

    /// <summary>
    /// Persists the batch, retrying transient failures with capped exponential backoff.
    /// </summary>
    private async Task<bool> PersistWithRetriesAsync(
        IConsumer<byte[], byte[]> consumer,
        List<ReadingToIngest> readings,
        CancellationToken stoppingToken
    )
    {
        for (var attempt = 1; attempt <= this.options.MaxBatchAttempts; attempt++)
        {
            try
            {
                await this.PersistAsync(readings, stoppingToken);
                return true;
            }
            catch (Exception ex) when (TransientFailureClassifier.IsTransient(ex))
            {
                if (attempt == this.options.MaxBatchAttempts)
                {
                    logger.BatchAbandoned(ex, attempt);
                    return false;
                }

                metrics.BatchRetries.Add(1);
                logger.BatchRetrying(ex, attempt, this.options.MaxBatchAttempts);

                await this.BackOffAsync(consumer, attempt, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Not recognised as transient, so retrying cannot help.
                logger.BatchFailedPermanently(ex);
                return false;
            }
        }

        return false;
    }

    private async Task PersistAsync(
        List<ReadingToIngest> readings,
        CancellationToken cancellationToken
    )
    {
        // An async scope, not a synchronous one: the unit of work is IAsyncDisposable only, and
        // disposing a scope containing such a service synchronously throws.
        await using var scope = scopeFactory.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var writeStopwatch = Stopwatch.StartNew();
        var result = await sender.SendAsync(
            new IngestReadingBatchCommand(readings),
            cancellationToken
        );
        metrics.DatabaseWriteDuration.Record(writeStopwatch.Elapsed.TotalSeconds);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Ingesting a batch failed: {result.Error}"
            );
        }

        metrics.ReadingsInserted.Add(result.Value.Inserted);
        metrics.DuplicatesSkipped.Add(result.Value.DuplicatesSkipped);
        logger.BatchPersisted(result.Value.Inserted, result.Value.DuplicatesSkipped);
    }

    /// <summary>
    /// Waits before the next attempt while keeping the partition assignment alive.
    /// <para>
    /// The partitions are paused first so that polling during the wait returns nothing instead of
    /// fetching messages this batch is not ready for. Polling cannot simply stop: exceeding
    /// <c>max.poll.interval.ms</c> has the broker evict this consumer mid-retry and hand its
    /// partitions to someone else.
    /// </para>
    /// </summary>
    private async Task BackOffAsync(
        IConsumer<byte[], byte[]> consumer,
        int attempt,
        CancellationToken stoppingToken
    )
    {
        var delayMs = Math.Min(
            this.options.RetryBaseDelayMs * Math.Pow(2, attempt - 1),
            this.options.RetryMaxDelayMs
        );
        var deadline = Stopwatch.StartNew();
        var budget = TimeSpan.FromMilliseconds(delayMs);

        var assignment = consumer.Assignment;

        if (assignment.Count > 0)
        {
            consumer.Pause(assignment);
        }

        try
        {
            while (deadline.Elapsed < budget && !stoppingToken.IsCancellationRequested)
            {
                // Returns null while paused, but still drives the poll loop and the group
                // coordinator.
                _ = consumer.Consume(PollTimeout);
            }
        }
        catch (ConsumeException ex)
        {
            logger.ConsumeFailed(ex, ex.Error.Reason);
        }
        finally
        {
            if (assignment.Count > 0)
            {
                // The assignment may have been revoked while paused, in which case resuming a
                // partition this consumer no longer owns is not an error worth failing over.
                try
                {
                    consumer.Resume(assignment);
                }
                catch (KafkaException ex)
                {
                    logger.ResumeFailed(ex, ex.Error.Reason);
                }
            }
        }
    }

    private async Task DeadLetterBatchAsync(
        List<ConsumeResult<byte[], byte[]>> batch,
        CancellationToken stoppingToken
    )
    {
        foreach (var message in batch)
        {
            await deadLetterProducer.SendAsync(
                message,
                "persist-failed",
                "The batch could not be persisted within the configured attempt limit.",
                stoppingToken
            );
        }
    }

    private void RecordLag(List<ReadingToIngest> readings)
    {
        var newest = readings.Max(reading => reading.CollectedAt);
        var lag = (DateTimeOffset.UtcNow - newest).TotalSeconds;

        if (lag >= 0)
        {
            metrics.ConsumerLag.Record(lag);
        }
    }

    /// <summary>
    /// Commits the highest offset seen per partition.
    /// <para>
    /// The enumerable overload of <c>Commit</c> takes the offset of the <em>next</em> message to
    /// read, so it needs the maximum consumed offset plus one. The single-result overload
    /// increments for the caller; this one does not.
    /// </para>
    /// </summary>
    private static void CommitOffsets(
        IConsumer<byte[], byte[]> consumer,
        List<ConsumeResult<byte[], byte[]>> batch
    )
    {
        var offsets = batch
            .GroupBy(message => message.TopicPartition)
            .Select(partition => new TopicPartitionOffset(
                partition.Key,
                new Offset(partition.Max(message => message.Offset.Value) + 1)
            ))
            .ToList();

        consumer.Commit(offsets);
    }
}
