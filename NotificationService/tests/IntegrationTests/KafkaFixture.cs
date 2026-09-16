namespace NotificationService.IntegrationTests;

using Confluent.Kafka;
using System.Text;
using Testcontainers.Kafka;

/// <summary>A throwaway Kafka broker shared by the hub tests.</summary>
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
    /// Publishes raw message bodies to a topic, mirroring what DataProcessorService produces.
    /// </summary>
    public async Task ProduceAsync(string topic, params string[] bodies)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = this.BootstrapAddress,
            Acks = Acks.All,
            EnableIdempotence = true,
        };

        using var producer = new ProducerBuilder<byte[], byte[]>(config).Build();

        foreach (var body in bodies)
        {
            await producer.ProduceAsync(
                topic,
                new Message<byte[], byte[]> { Value = Encoding.UTF8.GetBytes(body) }
            );
        }

        producer.Flush(TimeSpan.FromSeconds(10));
    }
}
