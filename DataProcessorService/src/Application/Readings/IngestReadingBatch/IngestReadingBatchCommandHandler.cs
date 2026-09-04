namespace DataProcessorService.Application.Readings.IngestReadingBatch;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Domain.Common;

/// <summary>
/// Resolves each reading's sensor to a surrogate key and writes the batch in one idempotent
/// statement.
/// </summary>
/// <param name="sensorRegistry">Resolves sensor natural keys to surrogate keys.</param>
/// <param name="repository">Writes readings.</param>
public sealed class IngestReadingBatchCommandHandler(
    ISensorRegistry sensorRegistry,
    IMeterReadingRepository repository
) : ICommandHandler<IngestReadingBatchCommand, IngestReadingBatchSummary>
{
    /// <inheritdoc/>
    public async Task<Result<IngestReadingBatchSummary>> HandleAsync(
        IngestReadingBatchCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Readings.Count == 0)
        {
            return Result.Success(new IngestReadingBatchSummary(0, 0));
        }

        var rows = new List<MeterReadingRow>(command.Readings.Count);

        foreach (var reading in command.Readings)
        {
            var sensorId = await sensorRegistry.GetOrCreateIdAsync(
                reading.SensorName,
                reading.SensorType,
                cancellationToken
            );

            rows.Add(
                new MeterReadingRow(
                    sensorId,
                    reading.SensorType,
                    UtcInstant.Normalize(reading.CollectedAt),
                    reading.Values
                )
            );
        }

        // Collapse duplicates inside the batch before handing it to the database. ON CONFLICT DO
        // NOTHING only defines behaviour against rows already committed, not against two rows
        // arriving in the same statement, and a seek-back retry can legitimately redeliver a
        // message that is already present in the batch being retried.
        var distinctRows = rows.DistinctBy(row => (row.SensorId, row.CollectedAtUtc)).ToList();

        var inserted = await repository.InsertIgnoringDuplicatesAsync(
            distinctRows,
            cancellationToken
        );

        return Result.Success(
            new IngestReadingBatchSummary(inserted, command.Readings.Count - inserted)
        );
    }
}
