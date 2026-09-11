namespace DataProcessorService.Application.Abstractions.Persistence;

/// <summary>
/// Groups a command's writes into one transaction, so a batch is either fully applied or not at
/// all. Driven by the unit-of-work pipeline behaviour, not called from handlers.
/// </summary>
public interface IUnitOfWork
{
    bool HasActiveTransaction { get; }

    Task BeginAsync(CancellationToken cancellationToken);

    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>Rolls back. Safe to call with no open transaction, so failure paths need no guard.</summary>
    Task RollbackAsync(CancellationToken cancellationToken);
}
