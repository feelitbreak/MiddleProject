namespace DataProcessorService.Application.Abstractions.Messaging;

using DataProcessorService.Domain.Common;

/// <summary>
/// The remainder of the pipeline, ending in the request's handler.
/// </summary>
/// <typeparam name="TResult">The result type produced.</typeparam>
/// <param name="cancellationToken">Token used to cancel the operation.</param>
/// <returns>The outcome of the rest of the pipeline.</returns>
public delegate Task<TResult> RequestHandlerDelegate<TResult>(CancellationToken cancellationToken)
    where TResult : Result;

/// <summary>
/// A cross-cutting concern wrapped around request handling, in registration order.
/// <para>
/// Registered as an open generic, so a behaviour's own generic constraints decide which requests
/// it applies to: a behaviour constrained to <see cref="IBaseCommand"/> is never constructed for a
/// query, because the dependency injection container skips constraint-violating closed types.
/// </para>
/// </summary>
/// <typeparam name="TRequest">The request type this behaviour applies to.</typeparam>
/// <typeparam name="TResult">The result type produced.</typeparam>
public interface IPipelineBehavior<in TRequest, TResult>
    where TRequest : IRequest<TResult>
    where TResult : Result
{
    /// <summary>Runs the behaviour around the rest of the pipeline.</summary>
    /// <param name="request">The request being handled.</param>
    /// <param name="next">The remainder of the pipeline.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome of the pipeline.</returns>
    Task<TResult> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResult> next,
        CancellationToken cancellationToken
    );
}
