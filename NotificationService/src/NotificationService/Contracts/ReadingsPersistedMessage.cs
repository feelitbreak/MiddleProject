namespace NotificationService.Contracts;

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
/// A duplicate of the contract DataProcessorService publishes, carried here rather than shared, per
/// the repository's no-shared-assemblies rule. It is also what reaches the browser: the hub passes
/// the event through rather than inventing a second shape, so the camelCase spelling
/// <see cref="JsonSerializerDefaults.Web"/> produces is part of both contracts at once.
/// </para>
/// </summary>
public sealed class ReadingsPersistedMessage(
    DateTimeOffset publishedAt,
    int readingCount,
    DateTimeOffset newestCollectedAt,
    IReadOnlyList<PersistedSensor>? sensors
)
{
    public static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web
    );

    public DateTimeOffset PublishedAt { get; } = publishedAt;

    /// <summary>Readings the batch inserted; duplicates it skipped are not counted.</summary>
    public int ReadingCount { get; } = readingCount;

    /// <summary>The newest collection instant in the batch, normalised as the database stores it.</summary>
    public DateTimeOffset NewestCollectedAt { get; } = newestCollectedAt;

    /// <summary>
    /// The distinct sensors the batch touched. Empty rather than null when the producer omits the
    /// array: the event is a signal to refetch, which stays useful without knowing what changed.
    /// </summary>
    public IReadOnlyList<PersistedSensor> Sensors { get; } = sensors ?? [];
}
