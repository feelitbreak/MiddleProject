namespace DataInjectorService.Services;

using Confluent.Kafka;
using DataInjectorService.Configuration;
using DataInjectorService.Models;
using DataInjectorService.Telemetry;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

/// <summary>
/// Kafka producer wrapper that serializes <see cref="MeterReading"/> objects to JSON
/// and publishes them to the configured topic.
/// </summary>
public sealed class KafkaProducer : IKafkaProducer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

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
            Acks = kafkaOptions.Acks switch
            {
                "All" => Acks.All,
                "Leader" => Acks.Leader,
                "None" => Acks.None,
                _ => Acks.All,
            },
            MaxInFlight = kafkaOptions.MaxInFlightRequestsPerConnection,
            EnableIdempotence = kafkaOptions.EnableIdempotence,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 500,
            MessageTimeoutMs = 30_000,
        };

        this.producer = new ProducerBuilder<string, string>(config).Build();
        this.logger.ProducerInitialised(kafkaOptions.BootstrapServers, this.topic);
    }

    /// <inheritdoc/>
    public async Task ProduceAsync(MeterReading reading, CancellationToken cancellationToken)
    {
        var key = $"{reading.Type}:{reading.Name}";
        var value = JsonSerializer.Serialize(reading, JsonOptions);

        var message = new Message<string, string> { Key = key, Value = value };

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
