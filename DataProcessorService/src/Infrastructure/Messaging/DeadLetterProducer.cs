namespace DataProcessorService.Infrastructure.Messaging;

using Confluent.Kafka;
using DataProcessorService.Infrastructure.Configuration;
using DataProcessorService.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;

/// <summary>
/// Publishes messages that can never be processed to the dead-letter topic, preserving the
/// original key and value bytes verbatim and recording why they were rejected.
/// </summary>
public interface IDeadLetterProducer : IAsyncDisposable
{
    /// <summary>Moves one message to the dead-letter topic.</summary>
    /// <param name="message">The original message, forwarded unchanged.</param>
    /// <param name="reason">A short machine-readable reason, used as a metric tag.</param>
    /// <param name="detail">A human-readable explanation, carried as a header.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes once the broker has acknowledged the message.</returns>
    Task SendAsync(
        ConsumeResult<byte[], byte[]> message,
        string reason,
        string detail,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Kafka-backed <see cref="IDeadLetterProducer"/>.
/// <para>
/// Produces with <c>Acks.All</c> and idempotence: a dead-letter that is silently lost would take
/// the offending message with it, since the consumer advances past the message once this returns.
/// </para>
/// </summary>
public sealed class DeadLetterProducer : IDeadLetterProducer
{
    /// <summary>Header carrying the topic the message was originally consumed from.</summary>
    public const string OriginalTopicHeader = "dlq-original-topic";

    /// <summary>Header carrying the partition the message was originally consumed from.</summary>
    public const string OriginalPartitionHeader = "dlq-original-partition";

    /// <summary>Header carrying the offset the message was originally consumed from.</summary>
    public const string OriginalOffsetHeader = "dlq-original-offset";

    /// <summary>Header carrying the short machine-readable rejection reason.</summary>
    public const string ReasonHeader = "dlq-reason";

    /// <summary>Header carrying the human-readable rejection detail.</summary>
    public const string DetailHeader = "dlq-detail";

    /// <summary>Header carrying the instant the message was dead-lettered.</summary>
    public const string TimestampHeader = "dlq-timestamp";

    private readonly IProducer<byte[], byte[]> producer;
    private readonly string topic;
    private readonly ILogger<DeadLetterProducer> logger;
    private readonly DataProcessorMetrics metrics;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeadLetterProducer"/> class.
    /// </summary>
    /// <param name="options">Kafka configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="metrics">Business metrics recorder.</param>
    public DeadLetterProducer(
        IOptions<KafkaOptions> options,
        ILogger<DeadLetterProducer> logger,
        DataProcessorMetrics metrics
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        this.logger = logger;
        this.metrics = metrics;

        var kafkaOptions = options.Value;
        this.topic = kafkaOptions.DeadLetterTopic;

        var config = new ProducerConfig
        {
            BootstrapServers = kafkaOptions.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 500,
            MessageTimeoutMs = 30_000,
        };

        this.producer = new ProducerBuilder<byte[], byte[]>(config).Build();
    }

    /// <inheritdoc/>
    public async Task SendAsync(
        ConsumeResult<byte[], byte[]> message,
        string reason,
        string detail,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(message);

        var headers = new Headers();

        foreach (var header in message.Message.Headers ?? [])
        {
            headers.Add(header);
        }

        headers.Add(OriginalTopicHeader, Encoding.UTF8.GetBytes(message.Topic));
        headers.Add(
            OriginalPartitionHeader,
            Encoding.UTF8.GetBytes(message.Partition.Value.ToString(CultureInfo.InvariantCulture))
        );
        headers.Add(
            OriginalOffsetHeader,
            Encoding.UTF8.GetBytes(message.Offset.Value.ToString(CultureInfo.InvariantCulture))
        );
        headers.Add(ReasonHeader, Encoding.UTF8.GetBytes(reason));
        headers.Add(DetailHeader, Encoding.UTF8.GetBytes(Truncate(detail)));
        headers.Add(
            TimestampHeader,
            Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture))
        );

        var deadLetter = new Message<byte[], byte[]>
        {
            Key = message.Message.Key,
            Value = message.Message.Value,
            Headers = headers,
        };

        await this.producer.ProduceAsync(this.topic, deadLetter, cancellationToken);

        this.metrics.DeadLetteredMessages.Add(
            1,
            new KeyValuePair<string, object?>("reason", reason)
        );

        this.logger.MessageDeadLettered(
            message.Topic,
            message.Partition.Value,
            message.Offset.Value,
            reason,
            detail
        );
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        this.producer.Flush(TimeSpan.FromSeconds(5));
        this.producer.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string Truncate(string detail) =>
        detail.Length <= 512 ? detail : detail[..512];
}
