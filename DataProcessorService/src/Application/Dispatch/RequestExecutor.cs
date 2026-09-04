namespace DataProcessorService.Application.Dispatch;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Executes one request type by resolving its handler and wrapping it in the registered pipeline
/// behaviours.
/// </summary>
/// <typeparam name="TRequest">The concrete request type.</typeparam>
/// <typeparam name="TResult">The result type produced.</typeparam>
internal sealed class RequestExecutor<TRequest, TResult> : IRequestExecutor<TResult>
    where TRequest : IRequest<TResult>
    where TResult : Result
{
    /// <inheritdoc/>
    public Task<TResult> ExecuteAsync(
        object request,
        IServiceProvider provider,
        CancellationToken cancellationToken
    )
    {
        var typedRequest = (TRequest)request;
        var handler = provider.GetRequiredService<IRequestHandler<TRequest, TResult>>();

        RequestHandlerDelegate<TResult> next = token => handler.HandleAsync(typedRequest, token);

        // Wrapped in reverse so that the first-registered behaviour ends up outermost.
        var behaviors = provider
            .GetServices<IPipelineBehavior<TRequest, TResult>>()
            .Reverse()
            .ToArray();

        foreach (var behavior in behaviors)
        {
            var inner = next;
            var current = behavior;
            next = token => current.HandleAsync(typedRequest, inner, token);
        }

        return next(cancellationToken);
    }
}
