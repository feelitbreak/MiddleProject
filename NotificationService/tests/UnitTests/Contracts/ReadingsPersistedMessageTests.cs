namespace NotificationService.UnitTests.Contracts;

using NotificationService.Contracts;
using System.Text.Json;

/// <summary>
/// Pins the wire contract. These property names are what DataProcessorService publishes *and* what
/// the React app binds to, since the hub passes the event through — renaming one breaks both ends at
/// once, and neither would fail to compile.
/// </summary>
public sealed class ReadingsPersistedMessageTests
{
    [Fact]
    public void Serialize_Always_WritesTheCamelCaseClientContract()
    {
        var message = new ReadingsPersistedMessage(
            new DateTimeOffset(2026, 9, 16, 9, 31, 25, 481, TimeSpan.Zero),
            18,
            new DateTimeOffset(2026, 9, 15, 9, 31, 23, 474, TimeSpan.Zero),
            [new PersistedSensor("Kitchen", "air_quality")]
        );

        var json = JsonSerializer.Serialize(
            message,
            ReadingsPersistedMessage.SerializerOptions
        );

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(
            ["publishedAt", "readingCount", "newestCollectedAt", "sensors"],
            root.EnumerateObject().Select(property => property.Name)
        );

        var sensor = root.GetProperty("sensors")[0];
        Assert.Equal(
            ["location", "sensorType"],
            sensor.EnumerateObject().Select(property => property.Name)
        );
        Assert.Equal("air_quality", sensor.GetProperty("sensorType").GetString());
    }

    [Fact]
    public void Serialize_Always_RoundTripsThroughTheSameOptions()
    {
        var message = new ReadingsPersistedMessage(
            DateTimeOffset.UtcNow,
            7,
            DateTimeOffset.UtcNow.AddSeconds(-1),
            [new PersistedSensor("Garage", "motion")]
        );

        var round = JsonSerializer.Deserialize<ReadingsPersistedMessage>(
            JsonSerializer.Serialize(message, ReadingsPersistedMessage.SerializerOptions),
            ReadingsPersistedMessage.SerializerOptions
        );

        Assert.NotNull(round);
        Assert.Equal(message.PublishedAt, round.PublishedAt);
        Assert.Equal(message.ReadingCount, round.ReadingCount);
        Assert.Equal(message.NewestCollectedAt, round.NewestCollectedAt);
        Assert.Equal("Garage", Assert.Single(round.Sensors).Location);
    }
}
