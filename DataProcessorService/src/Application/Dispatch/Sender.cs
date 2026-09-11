namespace DataProcessorService.Application.Dispatch;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Domain.Common;

/// <summary>
/// Default <see cref="ISender"/>: looks the request's runtime type up in the registry and hands
/// off to the matching executor.
/// </summary>
public sealed class Sender(RequestExecutorRegistry registry, IServiceProvider provider) : ISender
{
    /// <inheritdoc/>
    public Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return registry
            .Resolve<Result>(command.GetType())
            .ExecuteAsync(command, provider, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Result<TValue>> SendAsync<TValue>(
        ICommand<TValue> command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        return registry
            .Resolve<Result<TValue>>(command.GetType())
            .ExecuteAsync(command, provider, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Result<TValue>> SendAsync<TValue>(
        IQuery<TValue> query,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        return registry
            .Resolve<Result<TValue>>(query.GetType())
            .ExecuteAsync(query, provider, cancellationToken);
    }
}
