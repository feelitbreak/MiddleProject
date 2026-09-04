namespace DataInjectorService.Services;

using Confluent.Kafka;
using DataInjectorService.Configuration;
using DataInjectorService.Models;
using DataInjectorService.Telemetry;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

/// <summary>
/// Kafka producer wrapper that serializes <see cref="MeterReading"/> objects to JSON
/// and publishes them to the configured topic.
/// </summary>
public sealed class KafkaProducer : IKafkaProducer
{
    /// <summary>
    /// Version of the message contract carried on every produced message. Consumers use it to
    /// route or reject payloads whose shape they do not understand, so it must be incremented
    /// whenever <see cref="MeterReading"/>'s wire format changes incompatibly.
    /// </summary>
    private const string SchemaVersion = "1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    private static readonly byte[] ContentTypeHeaderValue = Encoding.UTF8.GetBytes(
        "application/json"
    );

    private static readonly byte[] SchemaVersionHeaderValue = Encoding.UTF8.GetBytes(
        SchemaVersion
    );

    private readonly IProducer<string, string> producer;
    private readonly string topic;
    private readonly ILogger<KafkaProducer> logger;
    private readonly DataInjectorMetrics metrics;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaProducer"/> class.
    /// </summary>
    /// <param name="options">Kafka configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="metrics">Business metrics recorder.</param>
    public KafkaProducer(
        IOptions<KafkaOptions> options,
        ILogger<KafkaProducer> logger,
        DataInjectorMetrics metrics
    )
    {
        this.logger = logger;
        this.metrics = metrics;
        var kafkaOptions = options.Value;
        this.topic = kafkaOptions.MeterReadingsTopic;

        var config = new ProducerConfig
        {
            BootstrapServers = kafkaOptions.BootstrapServers,
            Acks = kafkaOptions.Acks,
            CompressionType = kafkaOptions.CompressionType,
            MaxInFlight = kafkaOptions.MaxInFlightRequestsPerConnection,
            EnableIdempotence = kafkaOptions.EnableIdempotence,
            MessageSendMaxRetries = kafkaOptions.MessageSendMaxRetries,
            RetryBackoffMs = kafkaOptions.RetryBackoffMs,
            MessageTimeoutMs = kafkaOptions.MessageTimeoutMs,
        };

        this.producer = new ProducerBuilder<string, string>(config).Build();
        this.logger.ProducerInitialised(kafkaOptions.BootstrapServers, this.topic);
    }

    /// <inheritdoc/>
    public async Task ProduceAsync(MeterReading reading, CancellationToken cancellationToken)
    {
        var key = $"{reading.Type}:{reading.Name}";
        var value = JsonSerializer.Serialize(reading, JsonOptions);

        var message = new Message<string, string>
        {
            Key = key,
            Value = value,
            Headers =
            [
                new Header("content-type", ContentTypeHeaderValue),
                new Header("schema-version", SchemaVersionHeaderValue),
            ],
        };

        var stopwatch = Stopwatch.StartNew();
        var result = await this.producer.ProduceAsync(this.topic, message, cancellationToken);
        this.metrics.KafkaProduceDuration.Record(stopwatch.Elapsed.TotalSeconds);
        this.metrics.KafkaMessagesProduced.Add(1);

        this.logger.ReadingPublished(
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            key
        );
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        this.producer.Flush(TimeSpan.FromSeconds(5));
        this.producer.Dispose();
        return ValueTask.CompletedTask;
    }
}
