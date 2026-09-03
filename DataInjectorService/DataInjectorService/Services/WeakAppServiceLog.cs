namespace DataInjectorService.Services;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Log helper methods for <see cref="WeakAppService"/>.
/// Each method guards with <see cref="ILogger.IsEnabled"/> before constructing any arguments,
/// eliminating the CA1873 "possibly unnecessary argument evaluation" warning without requiring
/// source generation or partial methods.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class WeakAppServiceLog
{
    internal static void RequestTimedOut(
        this ILogger<WeakAppService> logger,
        Exception ex,
        string path
    )
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(ex, "Request to {Path} timed out", path);
        }
    }

    internal static void UnexpectedException(
        this ILogger<WeakAppService> logger,
        Exception ex,
        string path
    )
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(ex, "Unexpected exception calling {Path}", path);
        }
    }

    internal static void HealthCheckFailed(this ILogger<WeakAppService> logger, Exception ex)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(ex, "WeakApp health check failed");
        }
    }

    internal static void CorruptedData(this ILogger<WeakAppService> logger, string body)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning("WeakApp returned corrupted data: {Body}", body);
        }
    }

    internal static void DeserializationFailed(
        this ILogger<WeakAppService> logger,
        Exception ex,
        string body
    )
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(ex, "Failed to deserialize /meters response. Body: {Body}", body);
        }
    }

    internal static void EmptyMeterList(this ILogger<WeakAppService> logger)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning("WeakApp returned an empty meter list");
        }
    }

    internal static void ReadingsFetched(this ILogger<WeakAppService> logger, int count)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Fetched {Count} meter readings from WeakApp", count);
        }
    }

    internal static void RateLimited(this ILogger<WeakAppService> logger, double seconds)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                "WeakApp rate-limited this client. Backing off for {Seconds}s",
                seconds
            );
        }
    }

    internal static void ServerError(
        this ILogger<WeakAppService> logger,
        int statusCode,
        string? reason
    )
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                "WeakApp returned server error {StatusCode} {Reason}",
                statusCode,
                reason
            );
        }
    }

    internal static void UnexpectedStatus(this ILogger<WeakAppService> logger, int statusCode)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning("WeakApp returned unexpected status {StatusCode}", statusCode);
        }
    }
}
