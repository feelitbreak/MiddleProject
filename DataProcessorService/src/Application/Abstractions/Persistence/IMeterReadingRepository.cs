namespace DataProcessorService.Application.Abstractions.Persistence;

/// <summary>Writes readings into the meter readings table.</summary>
public interface IMeterReadingRepository
{
    /// <summary>
    /// Inserts <paramref name="rows"/>, skipping any that collide with an existing
    /// (sensor, collection instant) pair.
    /// <para>
    /// This is what makes consumption idempotent: Kafka delivers at least once, and the window
    /// between committing the database transaction and committing the offset means a crash
    /// redelivers a batch that was already written. Skipping collisions turns that redelivery into
    /// a no-op.
    /// </para>
    /// </summary>
    /// <param name="rows">The rows to insert.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// The number of rows actually inserted. Subtracting it from the input count gives the number
    /// of duplicates skipped.
    /// </returns>
    Task<int> InsertIgnoringDuplicatesAsync(
        IReadOnlyList<MeterReadingRow> rows,
        CancellationToken cancellationToken
    );
}
