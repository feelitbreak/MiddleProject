namespace DataProcessorService.Application.Readings.IngestReadingBatch;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Domain.Enums;

/// <summary>
/// One decoded reading awaiting persistence, still identified by its natural key because the
/// sensor's surrogate key is resolved during handling.
/// </summary>
public sealed class ReadingToIngest(
    string sensorName,
    SensorType sensorType,
    DateTimeOffset collectedAt,
    ReadingValues values
)
{
    public string SensorName { get; } = sensorName;

    public SensorType SensorType { get; } = sensorType;

    public DateTimeOffset CollectedAt { get; } = collectedAt;

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
public sealed class IngestReadingBatchCommand(IReadOnlyList<ReadingToIngest> readings)
    : ICommand<IngestReadingBatchSummary>
{
    public IReadOnlyList<ReadingToIngest> Readings { get; } = readings;
}

/// <summary>The outcome of persisting a batch.</summary>
public sealed class IngestReadingBatchSummary(int inserted, int duplicatesSkipped)
{
    public int Inserted { get; } = inserted;

    public int DuplicatesSkipped { get; } = duplicatesSkipped;
}
