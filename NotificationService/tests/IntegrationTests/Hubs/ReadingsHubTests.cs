namespace NotificationService.IntegrationTests.Hubs;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Hubs;
using NotificationService.Messaging;
using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;

/// <summary>
/// Drives the whole service against a real broker: a message on the topic, through the consumer and
/// the hub, to a subscribed client.
/// <para>
/// Nothing below the hub can prove this. The consumer, the JSON protocol's property naming and the
/// endpoint route only meet at runtime, and a client binding to <c>readingsChanged</c> is the one
/// place where getting any of them wrong shows up.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class ReadingsHubTests(KafkaFixture kafka) : IClassFixture<KafkaFixture>
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Consume_EventPublishedWhileConnected_ReachesTheClient()
    {
        var topic = NewTopic();

        // Published before the service starts, so it is both what creates the topic and the backlog
        // that AutoOffsetReset.Latest must skip. Its reading count marks it out below.
        await kafka.ProduceAsync(topic, Event(readingCount: 999, location: "Backlog"));

        await using var app = new NotificationApp(kafka.BootstrapAddress, topic);
        await using var client = await ConnectAsync(app);
        await WaitForPartitionAssignmentAsync(app);

        await kafka.ProduceAsync(topic, Event(readingCount: 18, location: "Kitchen"));

        var received = await ReadAsync(client.Events);

        // The contract the React app binds to, asserted on the wire rather than on a C# object.
        Assert.Equal(18, received.GetProperty("readingCount").GetInt32());
        Assert.Equal(
            DateTimeOffset.Parse("2026-09-16T09:31:25.481+00:00", CultureInfo.InvariantCulture),
            received.GetProperty("publishedAt").GetDateTimeOffset()
        );
        Assert.Equal(
            DateTimeOffset.Parse("2026-09-15T09:31:23.474+00:00", CultureInfo.InvariantCulture),
            received.GetProperty("newestCollectedAt").GetDateTimeOffset()
        );

        var sensor = Assert.Single(received.GetProperty("sensors").EnumerateArray());
        Assert.Equal("Kitchen", sensor.GetProperty("location").GetString());
        Assert.Equal("air_quality", sensor.GetProperty("sensorType").GetString());
    }

    [Fact]
    public async Task Consume_MessageFromBeforeStartup_IsNotReplayedToTheClient()
    {
        // Latest, not Earliest: a backlog of "something changed" signals describes changes a single
        // refetch already covers, and replaying them would stampede the gateway on every restart.
        var topic = NewTopic();

        await kafka.ProduceAsync(topic, Event(readingCount: 999, location: "Backlog"));

        await using var app = new NotificationApp(kafka.BootstrapAddress, topic);
        await using var client = await ConnectAsync(app);
        await WaitForPartitionAssignmentAsync(app);

        await kafka.ProduceAsync(topic, Event(readingCount: 18, location: "Kitchen"));

        // The backlog message precedes this one on the partition, so if it were being replayed it
        // would arrive first.
        var received = await ReadAsync(client.Events);
        Assert.Equal(18, received.GetProperty("readingCount").GetInt32());
    }

    [Fact]
    public async Task Consume_UndecodableMessage_DoesNotStopLaterEvents()
    {
        // The message is dropped rather than dead-lettered, so the only thing that must hold is
        // that it does not wedge the partition behind it.
        var topic = NewTopic();

        await kafka.ProduceAsync(topic, Event(readingCount: 999, location: "Backlog"));

        await using var app = new NotificationApp(kafka.BootstrapAddress, topic);
        await using var client = await ConnectAsync(app);
        await WaitForPartitionAssignmentAsync(app);

        await kafka.ProduceAsync(
            topic,
            "{ not json",
            "null",
            Event(readingCount: 4, location: "Garage")
        );

        var received = await ReadAsync(client.Events);
        Assert.Equal(4, received.GetProperty("readingCount").GetInt32());
    }

    private static string NewTopic() => $"meter-readings-persisted-{Guid.NewGuid():N}";

    private static string Event(int readingCount, string location) =>
        $$"""
        {"publishedAt":"2026-09-16T09:31:25.481+00:00","readingCount":{{readingCount}},
         "newestCollectedAt":"2026-09-15T09:31:23.474+00:00",
         "sensors":[{"location":"{{location}}","sensorType":"air_quality"}]}
        """;

    /// <summary>A subscribed client, with everything it has been pushed.</summary>
    private sealed class HubClient(HubConnection connection, ChannelReader<JsonElement> events)
        : IAsyncDisposable
    {
        public ChannelReader<JsonElement> Events { get; } = events;

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }

    /// <summary>Connects a client to the in-memory server and queues everything it is pushed.</summary>
    private static async Task<HubClient> ConnectAsync(NotificationApp app)
    {
        var events = Channel.CreateUnbounded<JsonElement>();

        var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(app.Server.BaseAddress, "hubs/readings"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
                    options.WebSocketFactory = async (context, cancellationToken) =>
                        await app.Server.CreateWebSocketClient()
                            .ConnectAsync(context.Uri, cancellationToken);
                }
            )
            .Build();

        // Cloned: the element the protocol hands over borrows a buffer that is recycled once the
        // handler returns.
        connection.On<JsonElement>(
            ReadingsHub.ReadingsChangedEvent,
            payload => events.Writer.TryWrite(payload.Clone())
        );

        await connection.StartAsync(TestContext.Current.CancellationToken);

        return new(connection, events.Reader);
    }

    /// <summary>
    /// Waits until the consumer owns a partition. Producing before that is lost under
    /// <c>AutoOffsetReset.Latest</c>, since there is no committed offset to fall back on.
    /// </summary>
    private static async Task WaitForPartitionAssignmentAsync(NotificationApp app)
    {
        var heartbeat = app.Services.GetRequiredService<ConsumerHeartbeat>();
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(60);

        while (!heartbeat.HasAssignment && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.True(heartbeat.HasAssignment, "The consumer never took a partition assignment.");

        // The assignment callback fires before librdkafka has resolved the end offset it starts
        // from, and a message produced in that window is neither in the backlog nor after it.
        await Task.Delay(1_000, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> ReadAsync(ChannelReader<JsonElement> events)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        timeout.CancelAfter(ReceiveTimeout);

        return await events.ReadAsync(timeout.Token);
    }

    /// <summary>The service under test, pointed at a throwaway broker and topic.</summary>
    private sealed class NotificationApp(string bootstrapServers, string topic)
        : WebApplicationFactory<ReadingsHub>
    {
        /// <inheritdoc/>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // Added after the application's own sources, so these win over appsettings.
            builder.ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Kafka:BootstrapServers"] = bootstrapServers,
                        ["Kafka:ReadingsPersistedTopic"] = topic,
                        ["Kafka:ConsumerGroupId"] = $"notification-service-{Guid.NewGuid():N}",
                    }
                )
            );
        }
    }
}
