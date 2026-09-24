namespace NotificationService.Messaging;

using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NotificationService.Configuration;
using NotificationService.Contracts;
using NotificationService.Hubs;
using NotificationService.Telemetry;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;
using System.Text;

/// <summary>
/// Consumes the readings-persisted topic and broadcasts each event to connected clients.
/// <para>
/// One message at a time, unlike DataProcessorService, which batches because it writes to
/// PostgreSQL. The processor already coalesces a whole poll into a single event, so batching here
/// would only add the linger window to the latency this service exists to minimise.
/// </para>
/// </summary>
public sealed class KafkaConsumerService(
    IHubContext<ReadingsHub> hubContext,
    IConsumerHeartbeat heartbeat,
    NotificationMetrics metrics,
    IOptions<KafkaOptions> options,
    ILogger<KafkaConsumerService> logger
) : BackgroundService
{
    /// <summary>
    /// How long a single poll blocks. Short on purpose: the loop interleaves the asynchronous
    /// broadcast between polls and must stay responsive to shutdown.
    /// </summary>
    private static readonly TimeSpan PollTimeout = TimeSpan.FromMilliseconds(100);

    public const string ActivitySourceName = NotificationMetrics.MeterName;

    private static readonly ActivitySource Activities = new(ActivitySourceName);

    private readonly KafkaOptions options = options.Value;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Must be the very first statement. StartAsync only returns once ExecuteAsync hits its
        // first *incomplete* await, so without this the subscribe-and-poll below would run
        // synchronously on the host start-up path and the web host would never bind its port.
        await Task.Yield();

        using var consumer = this.BuildConsumer();
        consumer.Subscribe(this.options.ReadingsPersistedTopic);
        logger.ConsumerStarted(this.options.ReadingsPersistedTopic, this.options.ConsumerGroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var message = this.Poll(consumer);

                heartbeat.Beat();
                heartbeat.SetAssignment(consumer.Assignment.Count > 0);

                if (message is null)
                {
                    continue;
                }

                await this.BroadcastAsync(message, stoppingToken);
                this.Commit(consumer, message);
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

            // Offsets advance only after an event has been broadcast.
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,

            // Latest, not the processor's Earliest: these events are signals to refetch, not data.
            // Replaying a backlog on first connect would broadcast hundreds of "something changed"
            // messages describing changes a single refetch of the gateway already covers.
            AutoOffsetReset = AutoOffsetReset.Latest,
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
    /// Polls once, returning <see langword="null"/> when nothing was available.
    /// </summary>
    private ConsumeResult<byte[], byte[]>? Poll(IConsumer<byte[], byte[]> consumer)
    {
        try
        {
            // The TimeSpan overload, never the CancellationToken one: it returns null cooperatively
            // when nothing is available, which is what keeps this loop responsive to shutdown.
            var result = consumer.Consume(PollTimeout);

            return result?.IsPartitionEOF == true ? null : result;
        }
        catch (ConsumeException ex)
        {
            logger.ConsumeFailed(ex, ex.Error.Reason);
            return null;
        }
    }

    /// <summary>
    /// Broadcasts one event to every connected client, passing the payload through as received.
    /// </summary>
    private async Task BroadcastAsync(
        ConsumeResult<byte[], byte[]> result,
        CancellationToken stoppingToken
    )
    {
        // One message at a time, so the processor's trace is a parent rather than a link.
        using var activity = Activities.StartActivity(
            "broadcast readings",
            ActivityKind.Consumer,
            Propagators
                .DefaultTextMapPropagator.Extract(default, result.Message.Headers, ReadHeader)
                .ActivityContext
        );

        metrics.EventsConsumed.Add(1);

        var decoded = ReadingsPersistedDecoder.Decode(result.Message.Value);

        if (decoded.IsFailure)
        {
            // Dropped rather than dead-lettered: the next batch publishes another signal within
            // seconds, so there is nothing here worth replaying.
            logger.MessageDropped(
                result.Topic,
                result.Partition.Value,
                result.Offset.Value,
                decoded.Error.Description
            );
            return;
        }

        var message = decoded.Value;

        await hubContext.Clients.All.SendAsync(
            ReadingsHub.ReadingsChangedEvent,
            message,
            stoppingToken
        );

        metrics.EventsPushed.Add(1);
        RecordLag(message);
        logger.EventBroadcast(message.ReadingCount, message.Sensors.Count);
    }

    /// <summary>
    /// Commits the event just broadcast. The single-result overload takes the consumed offset and
    /// increments it for the caller.
    /// </summary>
    private void Commit(IConsumer<byte[], byte[]> consumer, ConsumeResult<byte[], byte[]> message)
    {
        try
        {
            consumer.Commit(message);
        }
        catch (KafkaException ex)
        {
            // Not fatal: a redelivered signal costs one extra refetch, which consumers tolerate.
            logger.CommitFailed(ex, ex.Error.Reason);
        }
    }

    private static IEnumerable<string> ReadHeader(Headers? headers, string key) =>
        headers is not null && headers.TryGetLastBytes(key, out var value)
            ? [Encoding.UTF8.GetString(value)]
            : [];

    private void RecordLag(ReadingsPersistedMessage message)
    {
        var lag = (DateTimeOffset.UtcNow - message.PublishedAt).TotalSeconds;

        if (lag >= 0)
        {
            metrics.ConsumerLag.Record(lag);
        }
    }
}
