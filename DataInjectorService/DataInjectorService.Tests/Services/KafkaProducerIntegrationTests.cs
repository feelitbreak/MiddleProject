namespace DataInjectorService.Tests.Services;

using Confluent.Kafka;
using DataInjectorService.Configuration;
using DataInjectorService.Models;
using DataInjectorService.Services;
using DataInjectorService.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Testcontainers.Kafka;

/// <summary>
/// Exercises <see cref="KafkaProducer"/> against a real broker started in a container, because the
/// producer builds its <see cref="IProducer{TKey, TValue}"/> in the constructor and cannot be
/// driven through a mock. Covers the message key format, the serialized payload, the
/// <see cref="KafkaOptions.Acks"/> settings, and the flush performed on disposal.
/// <para>
/// Requires Docker. Filter these out with <c>--filter "Category!=Integration"</c> when no
/// container runtime is available.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class KafkaProducerIntegrationTests(KafkaContainerFixture fixture)
    : IClassFixture<KafkaContainerFixture>
{
    [Fact]
    public async Task ProduceAsync_ValidReading_MessageIsConsumable()
    {
        var topic = NewTopic();

        await using (var producer = this.BuildProducer(topic))
        {
            await producer.ProduceAsync(
                MakeReading("energy", "Kitchen"),
                TestContext.Current.CancellationToken
            );
        }

        var message = this.Consume(topic, count: 1)[0];

        Assert.Equal("energy:Kitchen", message.Message.Key);

        using var document = JsonDocument.Parse(message.Message.Value);
        var root = document.RootElement;

        Assert.Equal("energy", root.GetProperty("type").GetString());
        Assert.Equal("Kitchen", root.GetProperty("name").GetString());
        Assert.Equal(369.62, root.GetProperty("payload").GetProperty("energy").GetDouble());
        Assert.True(root.TryGetProperty("collectedAt", out _));
    }

    [Fact]
    public async Task ProduceAsync_SameSensor_UsesStableKeyAndPartition()
    {
        var topic = NewTopic();

        await using (var producer = this.BuildProducer(topic))
        {
            await producer.ProduceAsync(
                MakeReading("motion", "Office"),
                TestContext.Current.CancellationToken
            );
            await producer.ProduceAsync(
                MakeReading("motion", "Office"),
                TestContext.Current.CancellationToken
            );
        }

        var messages = this.Consume(topic, count: 2);

        Assert.All(messages, m => Assert.Equal("motion:Office", m.Message.Key));
        Assert.Equal(messages[0].Partition, messages[1].Partition);
    }

    [Fact]
    public async Task ProduceAsync_DifferentSensors_UseDistinctKeys()
    {
        var topic = NewTopic();

        await using (var producer = this.BuildProducer(topic))
        {
            await producer.ProduceAsync(
                MakeReading("energy", "Kitchen"),
                TestContext.Current.CancellationToken
            );
            await producer.ProduceAsync(
                MakeReading("air_quality", "Office"),
                TestContext.Current.CancellationToken
            );
        }

        var keys = this.Consume(topic, count: 2).Select(m => m.Message.Key).ToHashSet();

        Assert.Equal(["energy:Kitchen", "air_quality:Office"], keys);
    }

    /// <summary>
    /// Verifies every <see cref="KafkaOptions.Acks"/> value is accepted by the broker, by reading
    /// the produced message back off it. Idempotence is disabled for the non-All cases because
    /// librdkafka rejects a weaker acks setting while the idempotent producer is enabled --- a
    /// combination <see cref="KafkaOptions.Validate"/> now also rejects at startup.
    /// <para>
    /// There is no longer an "unrecognised value" case to cover: <see cref="KafkaOptions.Acks"/>
    /// is an enum, so an unknown value fails during configuration binding rather than silently
    /// falling back to <see cref="Acks.All"/>.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(Acks.All, true)]
    [InlineData(Acks.Leader, false)]
    [InlineData(Acks.None, false)]
    public async Task ProduceAsync_AcksSetting_IsAcceptedByBroker(
        Acks acks,
        bool enableIdempotence
    )
    {
        var topic = NewTopic();

        await using (var producer = this.BuildProducer(topic, acks, enableIdempotence))
        {
            await producer.ProduceAsync(
                MakeReading("energy", "Hall"),
                TestContext.Current.CancellationToken
            );
        }

        var messages = this.Consume(topic, count: 1);

        Assert.Equal("energy:Hall", messages[0].Message.Key);
    }

    [Fact]
    public async Task DisposeAsync_PendingMessage_IsFlushed()
    {
        var topic = NewTopic();

        var producer = this.BuildProducer(topic);
        await producer.ProduceAsync(MakeReading(), TestContext.Current.CancellationToken);

        await producer.DisposeAsync();

        var messages = this.Consume(topic, count: 1);

        Assert.Single(messages);
    }

    private static string NewTopic() => $"meter-readings-{Guid.NewGuid():N}";

    private static MeterReading MakeReading(string type = "energy", string name = "Hall") =>
        new()
        {
            Type = type,
            Name = name,
            Payload = new EnergyPayload { Energy = 369.62 },
        };

    private KafkaProducer BuildProducer(
        string topic,
        Acks acks = Acks.All,
        bool enableIdempotence = true
    )
    {
        var options = Options.Create(
            new KafkaOptions
            {
                BootstrapServers = fixture.BootstrapAddress,
                MeterReadingsTopic = topic,
                Acks = acks,
                EnableIdempotence = enableIdempotence,
            }
        );

        return new KafkaProducer(
            options,
            NullLogger<KafkaProducer>.Instance,
            new DataInjectorMetrics()
        );
    }

    /// <summary>
    /// Reads exactly <paramref name="count"/> messages from the beginning of
    /// <paramref name="topic"/> using a throwaway consumer group.
    /// </summary>
    private List<ConsumeResult<string, string>> Consume(string topic, int count)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = fixture.BootstrapAddress,
            GroupId = $"test-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>(count);
        while (messages.Count < count)
        {
            var result = consumer.Consume(TimeSpan.FromSeconds(30));
            Assert.NotNull(result);
            messages.Add(result);
        }

        consumer.Close();
        return messages;
    }
}

/// <summary>
/// Starts a single Kafka broker container shared by every test in
/// <see cref="KafkaProducerIntegrationTests"/>. xUnit creates a new test-class instance per test
/// method, so the container is owned by a class fixture to avoid paying the start-up cost each time.
/// </summary>
public sealed class KafkaContainerFixture : IAsyncLifetime
{
    /// <summary>Pinned so CI does not silently drift onto a new broker version.</summary>
    private const string KafkaImage = "confluentinc/cp-kafka:7.9.0";

    private readonly KafkaContainer kafka = new KafkaBuilder(KafkaImage).Build();

    /// <summary>Gets the bootstrap address of the running broker.</summary>
    public string BootstrapAddress => this.kafka.GetBootstrapAddress();

    /// <inheritdoc/>
    public ValueTask InitializeAsync() => new(this.kafka.StartAsync());

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => this.kafka.DisposeAsync();
}
