namespace DataProcessorService.Application.Abstractions.Messaging;

using DataProcessorService.Domain.Common;

/// <summary>
/// Dispatches a request to its registered handler, through the configured pipeline behaviours.
/// </summary>
public interface ISender
{
    /// <summary>Dispatches a command that returns no value.</summary>
    /// <param name="command">The command to dispatch.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome of handling the command.</returns>
    Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken);

    /// <summary>Dispatches a command that returns a value.</summary>
    /// <typeparam name="TValue">The value returned on success.</typeparam>
    /// <param name="command">The command to dispatch.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome of handling the command.</returns>
    Task<Result<TValue>> SendAsync<TValue>(
        ICommand<TValue> command,
        CancellationToken cancellationToken
    );

    /// <summary>Dispatches a query.</summary>
    /// <typeparam name="TValue">The value returned on success.</typeparam>
    /// <param name="query">The query to dispatch.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome of handling the query.</returns>
    Task<Result<TValue>> SendAsync<TValue>(
        IQuery<TValue> query,
        CancellationToken cancellationToken
    );
}
