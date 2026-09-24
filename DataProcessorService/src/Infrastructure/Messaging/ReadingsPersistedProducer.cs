namespace DataProcessorService.Infrastructure.Messaging;

using Confluent.Kafka;
using DataProcessorService.Infrastructure.Configuration;
using DataProcessorService.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

/// <summary>Announces that a batch of readings is committed and queryable.</summary>
public interface IReadingsPersistedProducer : IAsyncDisposable
{
    Task PublishAsync(ReadingsPersistedMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Kafka-backed <see cref="IReadingsPersistedProducer"/>. At-least-once and not transactional: an
/// event lost to a crash after the commit is replaced by the next batch seconds later, which is
/// why this carries no outbox table.
/// </summary>
public sealed class ReadingsPersistedProducer : IReadingsPersistedProducer
{
    private readonly IProducer<Null, byte[]> producer;
    private readonly string topic;
    private readonly ILogger<ReadingsPersistedProducer> logger;
    private readonly DataProcessorMetrics metrics;

    public ReadingsPersistedProducer(
        IOptions<KafkaOptions> options,
        ILogger<ReadingsPersistedProducer> logger,
        DataProcessorMetrics metrics
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        this.logger = logger;
        this.metrics = metrics;

        var kafkaOptions = options.Value;
        this.topic = kafkaOptions.ReadingsPersistedTopic;

        var config = new ProducerConfig
        {
            BootstrapServers = kafkaOptions.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 500,
            MessageTimeoutMs = 30_000,
        };

        this.producer = new ProducerBuilder<Null, byte[]>(config).Build();
    }

    /// <inheritdoc/>
    public async Task PublishAsync(
        ReadingsPersistedMessage message,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(message);

        var value = JsonSerializer.SerializeToUtf8Bytes(
            message,
            ReadingsPersistedMessage.SerializerOptions
        );

        var headers = new Headers();
        InjectTraceContext(headers);

        // Unkeyed: a broadcast signal, with no per-sensor ordering to preserve.
        await this.producer.ProduceAsync(
            this.topic,
            new Message<Null, byte[]> { Value = value, Headers = headers },
            cancellationToken
        );

        this.metrics.ReadingsPersistedEventsPublished.Add(1);
        this.logger.ReadingsPersisted(this.topic, message.ReadingCount, message.Sensors.Count);
    }

    /// <summary><see cref="Activity.Id"/> is the W3C traceparent verbatim, so no propagator.</summary>
    private static void InjectTraceContext(Headers headers)
    {
        var activity = Activity.Current;

        if (activity?.Id is not { } traceParent)
        {
            return;
        }

        headers.Add("traceparent", Encoding.UTF8.GetBytes(traceParent));

        if (activity.TraceStateString is { Length: > 0 } traceState)
        {
            headers.Add("tracestate", Encoding.UTF8.GetBytes(traceState));
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        this.producer.Flush(TimeSpan.FromSeconds(5));
        this.producer.Dispose();
        return ValueTask.CompletedTask;
    }
}
