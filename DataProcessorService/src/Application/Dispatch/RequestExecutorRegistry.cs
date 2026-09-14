namespace DataProcessorService.Application.Dispatch;

using DataProcessorService.Domain.Common;
using System.Collections.Frozen;

/// <summary>
/// Immutable map from request type to the executor that handles it, built once at startup.
/// </summary>
public sealed class RequestExecutorRegistry(IReadOnlyDictionary<Type, object> executors)
{
    private readonly FrozenDictionary<Type, object> executors = executors.ToFrozenDictionary();

    /// <summary>Gets every request type that has a registered handler.</summary>
    public IReadOnlyCollection<Type> RegisteredRequestTypes => this.executors.Keys;

    /// <summary>
    /// Resolves the executor for <paramref name="requestType"/>.
    /// </summary>
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
