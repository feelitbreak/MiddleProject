namespace DataProcessorService.Application.Dispatch;

using DataProcessorService.Domain.Common;

/// <summary>
/// Executes a request whose concrete type the caller does not know.
/// <para>
/// This is what keeps dispatch reflection-free. <c>ISender</c> knows only the result type, so
/// resolving the handler would otherwise need <c>MakeGenericType</c> --- which trips trimming
/// warnings under <c>TreatWarningsAsErrors</c>. Instead
/// <see cref="RequestExecutor{TRequest, TResult}"/> is built at the registration call site, where
/// every type argument is a compile-time parameter, leaving dispatch a dictionary lookup and one
/// interface call.
/// </para>
/// </summary>
internal interface IRequestExecutor<TResult>
    where TResult : Result
{
    Task<TResult> ExecuteAsync(
        object request,
        IServiceProvider provider,
        CancellationToken cancellationToken
    );
}
