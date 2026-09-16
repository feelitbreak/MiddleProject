namespace DataProcessorService.UnitTests.Messaging;

using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Readings.IngestReadingBatch;
using DataProcessorService.Domain.Enums;
using DataProcessorService.Infrastructure.Messaging;
using System.Text.Json;

/// <summary>
/// Covers the readings-persisted contract. NotificationService copies this shape rather than
/// sharing an assembly, so nothing but a test stops the copies drifting apart.
/// </summary>
public sealed class ReadingsPersistedMessageTests
{
    [Fact]
    public void ForBatch_RepeatedSensors_CoalescesToDistinctPairs()
    {
        var message = ReadingsPersistedMessage.ForBatch(
            [
                Reading("Kitchen", SensorType.AirQuality),
                Reading("Kitchen", SensorType.AirQuality),
                Reading("Kitchen", SensorType.Motion),
                Reading("Garage", SensorType.AirQuality),
            ],
            readingCount: 4,
            DateTimeOffset.UtcNow
        );

        Assert.Equal(
            [("Kitchen", "air_quality"), ("Kitchen", "motion"), ("Garage", "air_quality")],
            message.Sensors.Select(sensor => (sensor.Location, sensor.SensorType))
        );
    }

    [Fact]
    public void ForBatch_Batch_ReportsInsertedCountRatherThanBatchSize()
    {
        var message = ReadingsPersistedMessage.ForBatch(
            [Reading("Kitchen", SensorType.Motion), Reading("Garage", SensorType.Motion)],
            readingCount: 1,
            DateTimeOffset.UtcNow
        );

        Assert.Equal(1, message.ReadingCount);
    }

    [Fact]
    public void ForBatch_Batch_TakesTheNewestCollectedAtTruncatedToMicroseconds()
    {
        var newest = new DateTimeOffset(2026, 9, 15, 9, 31, 23, TimeSpan.Zero).AddTicks(4_748_627);

        var message = ReadingsPersistedMessage.ForBatch(
            [
                Reading("Kitchen", SensorType.Motion, newest.AddMinutes(-1)),
                Reading("Garage", SensorType.Motion, newest),
            ],
            readingCount: 2,
            DateTimeOffset.UtcNow
        );

        // Truncated as the database stores it, so a consumer can compare it against a queried row.
        Assert.Equal(newest.AddTicks(-7), message.NewestCollectedAt);
    }

    [Fact]
    public void Serialize_Message_MatchesTheConsumerContract()
    {
        var message = new ReadingsPersistedMessage(
            new DateTimeOffset(2026, 9, 16, 9, 31, 25, 481, TimeSpan.Zero),
            18,
            new DateTimeOffset(2026, 9, 15, 9, 31, 23, TimeSpan.Zero).AddTicks(4_748_620),
            [new PersistedSensor("Kitchen", "air_quality")]
        );

        var json = JsonSerializer.Serialize(message, ReadingsPersistedMessage.SerializerOptions);

        Assert.Equal(
            """
            {"publishedAt":"2026-09-16T09:31:25.481+00:00","readingCount":18,"newestCollectedAt":"2026-09-15T09:31:23.474862+00:00","sensors":[{"location":"Kitchen","sensorType":"air_quality"}]}
            """,
            json
        );
    }

    private static ReadingToIngest Reading(
        string location,
        SensorType type,
        DateTimeOffset? collectedAt = null
    ) => new(location, type, collectedAt ?? DateTimeOffset.UtcNow, Values(type));

    private static ReadingValues Values(SensorType type) =>
        type switch
        {
            SensorType.AirQuality => ReadingValues.ForAirQuality(415, 7, 44),
            SensorType.Motion => ReadingValues.ForMotion(true),
            _ => ReadingValues.ForEnergy(12.5),
        };
}
