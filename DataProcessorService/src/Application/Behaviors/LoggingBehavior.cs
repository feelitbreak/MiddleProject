namespace DataProcessorService.Application.Behaviors;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Domain.Common;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

/// <summary>
/// Logs the outcome and duration of every request. Applies to commands and queries alike, so it is
/// constrained only by <see cref="IRequest{TResult}"/>.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResult">The result type produced.</typeparam>
/// <param name="logger">Logger instance.</param>
public sealed class LoggingBehavior<TRequest, TResult>(
    ILogger<LoggingBehavior<TRequest, TResult>> logger
) : IPipelineBehavior<TRequest, TResult>
    where TRequest : IRequest<TResult>
    where TResult : Result
{
    /// <inheritdoc/>
    public async Task<TResult> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResult> next,
        CancellationToken cancellationToken
    )
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await next(cancellationToken);

            if (result.IsSuccess)
            {
                logger.RequestSucceeded(requestName, stopwatch.Elapsed.TotalMilliseconds);
            }
            else
            {
                logger.RequestFailed(
                    requestName,
                    result.Error.Code,
                    result.Error.Description,
                    stopwatch.Elapsed.TotalMilliseconds
                );
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.RequestThrew(ex, requestName, stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }
}
