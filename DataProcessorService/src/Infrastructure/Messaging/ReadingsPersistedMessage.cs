namespace DataProcessorService.Infrastructure.Messaging;

using DataProcessorService.Application.Readings.IngestReadingBatch;
using DataProcessorService.Domain.Common;
using DataProcessorService.Domain.Enums;
using System.Text.Json;

/// <summary>One sensor a committed batch wrote readings for.</summary>
public sealed class PersistedSensor(string location, string sensorType)
{
    /// <summary>The sensor's name, as stored in <c>sensors.name</c>.</summary>
    public string Location { get; } = location;

    /// <summary>The stored spelling: <c>air_quality</c>, <c>motion</c> or <c>energy</c>.</summary>
    public string SensorType { get; } = sensorType;
}

/// <summary>
/// The wire shape of a message on the readings-persisted topic:
/// <code>
/// {"publishedAt":"2026-09-16T09:31:25.481+00:00","readingCount":18,"newestCollectedAt":"2026-09-15T09:31:23.474862+00:00","sensors":[{"location":"Kitchen","sensorType":"motion"}]}
/// </code>
/// <para>
/// A public contract that consumers copy rather than share, so the camelCase spelling
/// <see cref="JsonSerializerDefaults.Web"/> produces is part of it.
/// </para>
/// </summary>
public sealed class ReadingsPersistedMessage(
    DateTimeOffset publishedAt,
    int readingCount,
    DateTimeOffset newestCollectedAt,
    IReadOnlyList<PersistedSensor> sensors
)
{
    public static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public DateTimeOffset PublishedAt { get; } = publishedAt;

    /// <summary>Readings the batch inserted; duplicates it skipped are not counted.</summary>
    public int ReadingCount { get; } = readingCount;

    /// <summary>The newest collection instant in the batch, normalised as the database stores it.</summary>
    public DateTimeOffset NewestCollectedAt { get; } = newestCollectedAt;

    public IReadOnlyList<PersistedSensor> Sensors { get; } = sensors;

    /// <summary>
    /// Coalesces a batch into its distinct (location, kind) pairs: the consumer only needs to know
    /// what changed, not which eighteen rows changed.
    /// </summary>
    /// <param name="readingCount">Readings actually inserted, not the size of the batch.</param>
    public static ReadingsPersistedMessage ForBatch(
        IReadOnlyList<ReadingToIngest> readings,
        int readingCount,
        DateTimeOffset publishedAt
    )
    {
        ArgumentNullException.ThrowIfNull(readings);

        var sensors = readings
            .DistinctBy(reading => (reading.SensorName, reading.SensorType))
            .Select(reading => new PersistedSensor(
                reading.SensorName,
                SensorTypeNames.ToName(reading.SensorType)
            ))
            .ToList();

        return new(
            publishedAt,
            readingCount,
            UtcInstant.Normalize(readings.Max(reading => reading.CollectedAt)),
            sensors
        );
    }
}
