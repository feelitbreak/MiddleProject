namespace DataProcessorService.Application.Abstractions.Persistence;

/// <summary>
/// Groups the writes performed while handling one command into a single database transaction, so
/// a batch is either fully applied or not applied at all. Driven by the unit-of-work pipeline
/// behaviour rather than called directly by handlers.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Gets a value indicating whether a transaction is currently open.</summary>
    bool HasActiveTransaction { get; }

    /// <summary>Opens a transaction.</summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes once the transaction is open.</returns>
    Task BeginAsync(CancellationToken cancellationToken);

    /// <summary>Commits the open transaction.</summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes once the transaction is committed.</returns>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Rolls the open transaction back. Safe to call when no transaction is open, so that failure
    /// paths need no additional guarding.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes once the transaction is rolled back.</returns>
    Task RollbackAsync(CancellationToken cancellationToken);
}
