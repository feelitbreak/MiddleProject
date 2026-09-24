namespace DataProcessorService.IntegrationTests.Messaging;

using Confluent.Kafka;
using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Behaviors;
using DataProcessorService.Application.Dispatch;
using DataProcessorService.Application.Readings.IngestReadingBatch;
using DataProcessorService.Domain.Entities;
using DataProcessorService.Infrastructure.Configuration;
using DataProcessorService.Infrastructure.Messaging;
using DataProcessorService.Infrastructure.Persistence;
using DataProcessorService.Infrastructure.Persistence.Repositories;
using DataProcessorService.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

/// <summary>
/// Drives the consumer against a real broker and a real database.
/// <para>
/// Two behaviours here cannot be verified any other way. The offset commit uses the enumerable
/// overload, which takes the offset of the <em>next</em> message rather than the last consumed one
/// --- an off-by-one that unit tests with a mocked consumer would happily reproduce wrongly. And
/// dead-lettering is what keeps one malformed message from blocking a partition permanently.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class KafkaConsumerServiceTests(PostgresFixture postgres, KafkaFixture kafka)
    : IClassFixture<PostgresFixture>,
        IClassFixture<KafkaFixture>
{
    [Fact]
    public async Task Consume_ValidMessages_PersistsEveryReading()
    {
        await postgres.ResetAsync();
        var topic = NewTopic();

        await kafka.ProduceAsync(
            topic,
            [
                ("energy:Kitchen", Reading("energy", "Kitchen", """{"energy":12.5}""")),
                (
                    "air_quality:Office",
                    Reading("air_quality", "Office", """{"co2":415,"pm25":7,"humidity":44}""")
                ),
                ("motion:Garage", Reading("motion", "Garage", """{"motionDetected":true}""")),
            ]
        );

        await RunConsumerUntilAsync(topic, () => CountReadingsAsync(3));

        await using var context = postgres.CreateContext();
        Assert.Equal(
            3,
            await context.MeterReadings.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(3, await context.Sensors.CountAsync(TestContext.Current.CancellationToken));

        var energy = await context
            .Set<EnergyReading>()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(12.5, energy.EnergyKwh);
    }

    [Fact]
    public async Task Consume_Redelivery_DoesNotDuplicateRows()
    {
        // The same messages consumed by a second, independent group must be a no-op, which is
        // exactly the situation a crash between the database commit and the offset commit creates.
        await postgres.ResetAsync();
        var topic = NewTopic();

        await kafka.ProduceAsync(
            topic,
            [("energy:Kitchen", Reading("energy", "Kitchen", """{"energy":1.0}"""))]
        );

        await RunConsumerUntilAsync(topic, () => CountReadingsAsync(1), groupId: NewGroup());
        await RunConsumerUntilAsync(topic, () => CountReadingsAsync(1), groupId: NewGroup());

        await using var context = postgres.CreateContext();
        Assert.Equal(
            1,
            await context.MeterReadings.CountAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Consume_PersistedBatch_AnnouncesOnceAfterTheRowsAreReadable()
    {
        await postgres.ResetAsync();
        var topic = NewTopic();
        var persistedTopic = $"{topic}-persisted";
        var collectedAt = DateTimeOffset.UtcNow;
        var rowsWereReadable = false;

        await kafka.ProduceAsync(
            topic,
            [
                (
                    "energy:Kitchen",
                    Reading("energy", "Kitchen", """{"energy":12.5}""", collectedAt)
                ),
                (
                    "motion:Kitchen",
                    Reading("motion", "Kitchen", """{"motionDetected":true}""", collectedAt)
                ),
                (
                    "motion:Kitchen",
                    Reading(
                        "motion",
                        "Kitchen",
                        """{"motionDetected":false}""",
                        collectedAt.AddMinutes(1)
                    )
                ),
                (
                    "motion:Garage",
                    Reading("motion", "Garage", """{"motionDetected":true}""", collectedAt)
                ),
            ]
        );

        await RunConsumerUntilAsync(
            topic,
            async () =>
            {
                if (kafka.Consume(persistedTopic, count: 1, TimeSpan.FromSeconds(1)).Count == 0)
                {
                    return false;
                }

                // The event exists, so the transaction behind it must already have committed.
                rowsWereReadable = await CountReadingsAsync(4);
                return true;
            }
        );

        Assert.True(rowsWereReadable);

        // One event for the batch, not one per reading.
        var announced = kafka.Consume(persistedTopic, count: 2, TimeSpan.FromSeconds(10));
        var message = Assert.Single(announced);

        var payload = JsonSerializer.Deserialize<ReadingsPersistedMessage>(
            message.Message.Value,
            ReadingsPersistedMessage.SerializerOptions
        );

        Assert.NotNull(payload);
        Assert.Equal(4, payload.ReadingCount);
        Assert.Equal(
            [("Kitchen", "energy"), ("Kitchen", "motion"), ("Garage", "motion")],
            payload.Sensors.Select(sensor => (sensor.Location, sensor.SensorType))
        );
    }

    [Fact]
    public async Task Consume_Redelivery_AnnouncesNothing()
    {
        // A redelivered batch inserts nothing, and announcing it would have every connected client
        // refetch unchanged data.
        await postgres.ResetAsync();
        var topic = NewTopic();
        var persistedTopic = $"{topic}-persisted";

        await kafka.ProduceAsync(
            topic,
            [("energy:Kitchen", Reading("energy", "Kitchen", """{"energy":1.0}"""))]
        );

        await RunConsumerUntilAsync(topic, () => CountReadingsAsync(1), groupId: NewGroup());
        await RunConsumerUntilAsync(topic, () => CountReadingsAsync(1), groupId: NewGroup());

        var announced = kafka.Consume(persistedTopic, count: 2, TimeSpan.FromSeconds(15));
        Assert.Single(announced);
    }

    [Fact]
    public async Task Consume_CommitsNextOffsetNotLastConsumed()
    {
        await postgres.ResetAsync();
        var topic = NewTopic();
        var group = NewGroup();

        var delivered = await kafka.ProduceAsync(
            topic,
            Enumerable
                .Range(0, 5)
                .Select(index =>
                    (
                        $"energy:Sensor{index}",
                        Reading("energy", $"Sensor{index}", $$"""{"energy":{{index}}.0}""")
                    )
                )
        );

        await RunConsumerUntilAsync(topic, () => CountReadingsAsync(5), groupId: group);

        var committed = kafka.CommittedOffsets(topic, group, partitionCount: 1);
        var highestProduced = delivered.Max(result => result.Offset.Value);

        // Commit(IEnumerable<TopicPartitionOffset>) records where to resume, not what was read.
        Assert.Equal(highestProduced + 1, committed[0]);
    }

    [Fact]
    public async Task Consume_UndecodableMessage_IsDeadLetteredAndDoesNotBlockTheBatch()
    {
        await postgres.ResetAsync();
        var topic = NewTopic();
        var deadLetterTopic = $"{topic}-dlq";

        await kafka.ProduceAsync(
            topic,
            [
                ("bad:1", Encoding.UTF8.GetBytes("{ not json")),
                ("temperature:Attic", Reading("temperature", "Attic", """{"celsius":21}""")),
                ("energy:Kitchen", Reading("energy", "Kitchen", """{"energy":9.5}""")),
            ]
        );

        await RunConsumerUntilAsync(
            topic,
            () => CountReadingsAsync(1),
            deadLetterTopic: deadLetterTopic
        );

        // The valid reading still landed, so the poison messages did not block the batch.
        await using var context = postgres.CreateContext();
        Assert.Equal(
            1,
            await context.MeterReadings.CountAsync(TestContext.Current.CancellationToken)
        );

        var deadLettered = kafka.Consume(deadLetterTopic, count: 2, TimeSpan.FromSeconds(30));
        Assert.Equal(2, deadLettered.Count);

        foreach (var message in deadLettered)
        {
            var reason = HeaderValue(message, DeadLetterProducer.ReasonHeader);
            Assert.Equal("decode-failed", reason);
            Assert.NotNull(HeaderValue(message, DeadLetterProducer.DetailHeader));
            Assert.Equal(topic, HeaderValue(message, DeadLetterProducer.OriginalTopicHeader));
        }
    }

    [Fact]
    public async Task Consume_DeadLetteredMessage_PreservesOriginalBytes()
    {
        await postgres.ResetAsync();
        var topic = NewTopic();
        var deadLetterTopic = $"{topic}-dlq";
        var original = Encoding.UTF8.GetBytes("{ not json");

        await kafka.ProduceAsync(topic, [("bad:1", original)]);

        await RunConsumerUntilAsync(
            topic,
            () => Task.FromResult(true),
            deadLetterTopic: deadLetterTopic,
            settleFor: TimeSpan.FromSeconds(8)
        );

        var deadLettered = kafka.Consume(deadLetterTopic, count: 1, TimeSpan.FromSeconds(30));

        var message = Assert.Single(deadLettered);
        Assert.Equal(original, message.Message.Value);
        Assert.Equal("bad:1", Encoding.UTF8.GetString(message.Message.Key));
    }

    private static string NewTopic() => $"meter-readings-{Guid.NewGuid():N}";

    private static string NewGroup() => $"data-processor-{Guid.NewGuid():N}";

    private static byte[] Reading(
        string type,
        string name,
        string payload,
        DateTimeOffset? collectedAt = null
    ) =>
        Encoding.UTF8.GetBytes(
            $$"""
            {"type":"{{type}}","name":"{{name}}","payload":{{payload}},"collectedAt":"{{collectedAt
                ?? DateTimeOffset.UtcNow:O}}"}
            """
        );

    private static string? HeaderValue(ConsumeResult<byte[], byte[]> message, string key) =>
        message.Message.Headers.TryGetLastBytes(key, out var value)
            ? Encoding.UTF8.GetString(value)
            : null;

    private async Task<bool> CountReadingsAsync(int expected)
    {
        await using var context = postgres.CreateContext();
        return await context.MeterReadings.CountAsync(TestContext.Current.CancellationToken)
            >= expected;
    }

    /// <summary>
    /// Runs the consumer until <paramref name="until"/> reports success, then stops it cleanly.
    /// </summary>
    private async Task RunConsumerUntilAsync(
        string topic,
        Func<Task<bool>> until,
        string? groupId = null,
        string? deadLetterTopic = null,
        TimeSpan? settleFor = null
    )
    {
        var options = new KafkaOptions
        {
            BootstrapServers = kafka.BootstrapAddress,
            MeterReadingsTopic = topic,
            DeadLetterTopic = deadLetterTopic ?? $"{topic}-dlq",
            ReadingsPersistedTopic = $"{topic}-persisted",
            ConsumerGroupId = groupId ?? NewGroup(),
            MaxBatchSize = 100,
            BatchLingerMs = 500,
            MaxBatchAttempts = 2,
            RetryBaseDelayMs = 100,
            RetryMaxDelayMs = 500,
        };

        await using var provider = BuildProvider(options);
        var consumer = ActivatorUtilities.CreateInstance<KafkaConsumerService>(provider);

        await consumer.StartAsync(CancellationToken.None);

        try
        {
            var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(60);

            while (DateTimeOffset.UtcNow < deadline && !await until())
            {
                await Task.Delay(250, TestContext.Current.CancellationToken);
            }

            if (settleFor is not null)
            {
                await Task.Delay(settleFor.Value, TestContext.Current.CancellationToken);
            }
            else
            {
                // Offsets are committed after the database write, so allow the cycle to finish.
                await Task.Delay(1_500, TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    private ServiceProvider BuildProvider(KafkaOptions kafkaOptions)
    {
        var services = new ServiceCollection();

        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(Options.Create(kafkaOptions));
        services.AddDbContext<MeterReadingsDbContext>(options =>
            options.UseNpgsql(postgres.ConnectionString)
        );
        services.AddScoped<IMeterReadingRepository, MeterReadingRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<ISensorRegistry, SensorRegistry>();
        services.AddSingleton<DataProcessorMetrics>();
        services.AddSingleton<IConsumerHeartbeat, ConsumerHeartbeat>();
        services.AddSingleton<IDeadLetterProducer, DeadLetterProducer>();
        services.AddSingleton<IReadingsPersistedProducer, ReadingsPersistedProducer>();

        services.AddCqrs(cqrs =>
            cqrs.AddBehavior(typeof(UnitOfWorkBehavior<,>))
                .AddCommandHandler<
                    IngestReadingBatchCommand,
                    IngestReadingBatchSummary,
                    IngestReadingBatchCommandHandler
                >()
        );

        return services.BuildServiceProvider();
    }
}
