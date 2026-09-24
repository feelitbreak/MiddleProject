namespace DataProcessorService.IntegrationTests;

using Confluent.Kafka;
using Testcontainers.Kafka;

/// <summary>A throwaway Kafka broker shared by the messaging tests.</summary>
public sealed class KafkaFixture : IAsyncLifetime
{
    private const string KafkaImage = "confluentinc/cp-kafka:7.9.0";

    private readonly KafkaContainer kafka = new KafkaBuilder(KafkaImage).Build();

    /// <summary>Gets the bootstrap address of the running broker.</summary>
    public string BootstrapAddress => this.kafka.GetBootstrapAddress();

    /// <inheritdoc/>
    public ValueTask InitializeAsync() => new(this.kafka.StartAsync());

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => this.kafka.DisposeAsync();

    /// <summary>
    /// Publishes raw message bodies to a topic, mirroring what DataInjectorService produces.
    /// </summary>
    public Task<IReadOnlyList<DeliveryResult<byte[], byte[]>>> ProduceAsync(
        string topic,
        IEnumerable<(string Key, byte[] Body)> messages
    ) => this.ProduceAsync(topic, messages.Select(message => (message.Key, message.Body, (Headers?)null)));

    public async Task<IReadOnlyList<DeliveryResult<byte[], byte[]>>> ProduceAsync(
        string topic,
        IEnumerable<(string Key, byte[] Body, Headers? Headers)> messages
    )
    {
        var config = new ProducerConfig
        {
            BootstrapServers = this.BootstrapAddress,
            Acks = Acks.All,
            EnableIdempotence = true,
        };

        using var producer = new ProducerBuilder<byte[], byte[]>(config).Build();

        var results = new List<DeliveryResult<byte[], byte[]>>();

        foreach (var (key, body, headers) in messages)
        {
            results.Add(
                await producer.ProduceAsync(
                    topic,
                    new Message<byte[], byte[]>
                    {
                        Key = System.Text.Encoding.UTF8.GetBytes(key),
                        Value = body,
                        Headers = headers,
                    }
                )
            );
        }

        producer.Flush(TimeSpan.FromSeconds(10));
        return results;
    }

    /// <summary>Reads up to <paramref name="count"/> messages from the start of a topic.</summary>
    public IReadOnlyList<ConsumeResult<byte[], byte[]>> Consume(
        string topic,
        int count,
        TimeSpan timeout
    )
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = this.BootstrapAddress,
            GroupId = $"assert-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<byte[], byte[]>(config).Build();
        consumer.Subscribe(topic);

        var consumed = new List<ConsumeResult<byte[], byte[]>>();
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (consumed.Count < count && DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromMilliseconds(500));

                if (result is not null && !result.IsPartitionEOF)
                {
                    consumed.Add(result);
                }
            }
            catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
            {
                // A topic only exists once something has produced to it, and callers poll for
                // messages a service under test has not published yet.
            }
        }

        consumer.Close();
        return consumed;
    }

    /// <summary>Reads the committed offsets for a consumer group on a topic.</summary>
    public IReadOnlyDictionary<int, long> CommittedOffsets(
        string topic,
        string groupId,
        int partitionCount
    )
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = this.BootstrapAddress,
            GroupId = groupId,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<byte[], byte[]>(config).Build();

        var partitions = Enumerable
            .Range(0, partitionCount)
            .Select(partition => new TopicPartition(topic, new Partition(partition)))
            .ToList();

        return consumer
            .Committed(partitions, TimeSpan.FromSeconds(15))
            .ToDictionary(offset => offset.Partition.Value, offset => offset.Offset.Value);
    }
}
