namespace DataProcessorService.Application.Readings.IngestReadingBatch;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Domain.Enums;

/// <summary>
/// One decoded reading awaiting persistence, still identified by its natural key because the
/// sensor's surrogate key is resolved during handling.
/// </summary>
/// <param name="sensorName">The location the reading came from.</param>
/// <param name="sensorType">The kind of sensor that produced it.</param>
/// <param name="collectedAt">
/// When the injector collected the reading. Normalised to UTC during handling.
/// </param>
/// <param name="values">The type-specific values.</param>
public sealed class ReadingToIngest(
    string sensorName,
    SensorType sensorType,
    DateTimeOffset collectedAt,
    ReadingValues values
)
{
    /// <summary>Gets the location the reading came from.</summary>
    public string SensorName { get; } = sensorName;

    /// <summary>Gets the kind of sensor that produced the reading.</summary>
    public SensorType SensorType { get; } = sensorType;

    /// <summary>Gets the instant the injector collected the reading.</summary>
    public DateTimeOffset CollectedAt { get; } = collectedAt;

    /// <summary>Gets the type-specific values.</summary>
    public ReadingValues Values { get; } = values;
}

/// <summary>
/// Persists a batch of readings.
/// <para>
/// Deliberately one command per Kafka batch rather than per message: the unit-of-work behaviour
/// then means "one transaction per batch", and a batch of several hundred readings costs one
/// dispatch and one round trip instead of several hundred of each.
/// </para>
/// </summary>
/// <param name="readings">The readings to persist.</param>
public sealed class IngestReadingBatchCommand(IReadOnlyList<ReadingToIngest> readings)
    : ICommand<IngestReadingBatchSummary>
{
    /// <summary>Gets the readings to persist.</summary>
    public IReadOnlyList<ReadingToIngest> Readings { get; } = readings;
}

/// <summary>The outcome of persisting a batch.</summary>
/// <param name="inserted">How many rows were newly written.</param>
/// <param name="duplicatesSkipped">
/// How many rows collided with an existing (sensor, collection instant) pair and were skipped,
/// whether within the batch itself or against rows already stored.
/// </param>
public sealed class IngestReadingBatchSummary(int inserted, int duplicatesSkipped)
{
    /// <summary>Gets the number of rows newly written.</summary>
    public int Inserted { get; } = inserted;

    /// <summary>Gets the number of rows skipped as duplicates.</summary>
    public int DuplicatesSkipped { get; } = duplicatesSkipped;
}
