namespace DataProcessorService.Application.Abstractions.Persistence;

/// <summary>Writes readings into the meter readings table.</summary>
public interface IMeterReadingRepository
{
    /// <summary>
    /// Inserts <paramref name="rows"/>, skipping any that collide with an existing
    /// (sensor, collection instant) pair, and returns how many were actually written.
    /// <para>
    /// This is what makes consumption idempotent. Kafka delivers at least once, and a crash between
    /// the database commit and the offset commit redelivers a batch that is already stored.
    /// </para>
    /// </summary>
    Task<int> InsertIgnoringDuplicatesAsync(
        IReadOnlyList<MeterReadingRow> rows,
        CancellationToken cancellationToken
    );
}
