namespace DataProcessorService.Application.Behaviors;

using DataProcessorService.Domain.Common;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Logging extensions for <see cref="LoggingBehavior{TRequest, TResult}"/>.
/// <para>
/// Written as extension methods that check <see cref="ILogger.IsEnabled"/> before formatting,
/// which satisfies CA1873 without pulling in source-generated logging --- the same approach the
/// sibling DataInjectorService uses.
/// </para>
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Logging message definitions: no branching behaviour to cover.")]
internal static class LoggingBehaviorLog
{
    /// <summary>Logs a successfully handled request.</summary>
    internal static void RequestSucceeded<TRequest, TResult>(
        this ILogger<LoggingBehavior<TRequest, TResult>> logger,
        string requestName,
        double elapsedMs
    )
        where TRequest : Abstractions.Messaging.IRequest<TResult>
        where TResult : Result
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "{Request} handled successfully in {ElapsedMs:F1} ms",
                requestName,
                elapsedMs
            );
        }
    }

    /// <summary>Logs a request that returned a failed result.</summary>
    internal static void RequestFailed<TRequest, TResult>(
        this ILogger<LoggingBehavior<TRequest, TResult>> logger,
        string requestName,
        ErrorCode code,
        string description,
        double elapsedMs
    )
        where TRequest : Abstractions.Messaging.IRequest<TResult>
        where TResult : Result
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                "{Request} failed with {ErrorCode}: {ErrorDescription} after {ElapsedMs:F1} ms",
                requestName,
                code,
                description,
                elapsedMs
            );
        }
    }

    /// <summary>Logs a request whose handler threw.</summary>
    internal static void RequestThrew<TRequest, TResult>(
        this ILogger<LoggingBehavior<TRequest, TResult>> logger,
        Exception exception,
        string requestName,
        double elapsedMs
    )
        where TRequest : Abstractions.Messaging.IRequest<TResult>
        where TResult : Result
    {
        if (logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(
                exception,
                "{Request} threw after {ElapsedMs:F1} ms",
                requestName,
                elapsedMs
            );
        }
    }
}
