namespace DataProcessorService.Application.Dispatch;

using DataProcessorService.Domain.Common;

/// <summary>
/// Executes a request whose concrete type is not known to the caller.
/// <para>
/// This is the seam that keeps dispatch reflection-free. <c>ISender</c> knows only the result
/// type, never the concrete request type, so resolving <c>IRequestHandler&lt;TRequest,
/// TResult&gt;</c> would normally require <c>MakeGenericType</c> --- which trips trimming warnings
/// under <c>TreatWarningsAsErrors</c> and only fails at runtime when a handler is missing.
/// Instead, <see cref="RequestExecutor{TRequest, TResult}"/> is constructed at the registration
/// call site, where every type argument is a compile-time generic parameter, and stored against
/// the request type. Dispatch is then a dictionary lookup, a cast, and one interface call.
/// </para>
/// </summary>
/// <typeparam name="TResult">The result type produced.</typeparam>
internal interface IRequestExecutor<TResult>
    where TResult : Result
{
    /// <summary>Executes the request through its handler and pipeline behaviours.</summary>
    /// <param name="request">The request, whose runtime type the implementation knows.</param>
    /// <param name="provider">Scoped service provider used to resolve the handler and behaviours.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome of handling the request.</returns>
    Task<TResult> ExecuteAsync(
        object request,
        IServiceProvider provider,
        CancellationToken cancellationToken
    );
}
