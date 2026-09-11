namespace DataProcessorService.Application.Abstractions.Messaging;

using DataProcessorService.Domain.Common;

/// <summary>
/// Dispatches a request to its registered handler, through the configured pipeline behaviours.
/// </summary>
public interface ISender
{
    /// <summary>Dispatches a command that returns no value.</summary>
    Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken);

    /// <summary>Dispatches a command that returns a value.</summary>
    Task<Result<TValue>> SendAsync<TValue>(
        ICommand<TValue> command,
        CancellationToken cancellationToken
    );

    /// <summary>Dispatches a query.</summary>
    Task<Result<TValue>> SendAsync<TValue>(
        IQuery<TValue> query,
        CancellationToken cancellationToken
    );
}
