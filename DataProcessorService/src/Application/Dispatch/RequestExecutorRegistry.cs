namespace DataProcessorService.Application.Dispatch;

using DataProcessorService.Domain.Common;
using System.Collections.Frozen;

/// <summary>
/// Immutable map from request type to the executor that handles it, built once at startup.
/// </summary>
/// <param name="executors">Executors keyed by request type.</param>
public sealed class RequestExecutorRegistry(IReadOnlyDictionary<Type, object> executors)
{
    private readonly FrozenDictionary<Type, object> executors = executors.ToFrozenDictionary();

    /// <summary>Gets every request type that has a registered handler.</summary>
    public IReadOnlyCollection<Type> RegisteredRequestTypes => this.executors.Keys;

    /// <summary>
    /// Resolves the executor for <paramref name="requestType"/>.
    /// </summary>
    /// <typeparam name="TResult">The result type the request produces.</typeparam>
    /// <param name="requestType">The runtime type of the request being dispatched.</param>
    /// <returns>The executor registered for that request type.</returns>
    /// <exception cref="InvalidOperationException">No handler is registered for the request type.</exception>
    internal IRequestExecutor<TResult> Resolve<TResult>(Type requestType)
        where TResult : Result
    {
        if (!this.executors.TryGetValue(requestType, out var executor))
        {
            throw new InvalidOperationException(
                $"No handler is registered for request type '{requestType}'. "
                    + "Register it with AddCommandHandler or AddQueryHandler in AddCqrs."
            );
        }

        return (IRequestExecutor<TResult>)executor;
    }
}
