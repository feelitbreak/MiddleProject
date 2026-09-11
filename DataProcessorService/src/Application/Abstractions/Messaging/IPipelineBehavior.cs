namespace DataProcessorService.Application.Abstractions.Messaging;

using DataProcessorService.Domain.Common;

/// <summary>
/// The remainder of the pipeline, ending in the request's handler.
/// </summary>
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
public interface IPipelineBehavior<in TRequest, TResult>
    where TRequest : IRequest<TResult>
    where TResult : Result
{
    /// <summary>Runs the behaviour around the rest of the pipeline.</summary>
    Task<TResult> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResult> next,
        CancellationToken cancellationToken
    );
}
