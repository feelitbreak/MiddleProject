namespace DataInjectorService.Services;

using DataInjectorService.Common;

/// <summary>
/// Log helper methods for <see cref="MeterPollingService"/>.
/// Each method guards with <see cref="ILogger.IsEnabled"/> before constructing any arguments,
/// eliminating the CA1873 "possibly unnecessary argument evaluation" warning without requiring
/// source generation or partial methods.
/// </summary>
internal static class MeterPollingServiceLog
{
    internal static void ServiceStarted(this ILogger<MeterPollingService> logger, int interval)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "MeterPollingService started. Polling interval: {Interval}s",
                interval
            );
        }
    }

    internal static void ServiceStopped(this ILogger<MeterPollingService> logger)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("MeterPollingService stopped");
        }
    }

    internal static void UnhandledPollingException(
        this ILogger<MeterPollingService> logger,
        Exception ex
    )
    {
        if (logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(ex, "Unhandled exception in polling loop; continuing after delay");
        }
    }

    internal static void RateLimited(this ILogger<MeterPollingService> logger, double seconds)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning("Rate limited by WeakApp; backing off for {Seconds}s", seconds);
        }
    }

    internal static void UnusableData(
        this ILogger<MeterPollingService> logger,
        ErrorCode code,
        string description
    )
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                "WeakApp call did not return usable data. Code: {Code}, Description: {Description}",
                code,
                description
            );
        }
    }

    internal static void ProduceFailed(
        this ILogger<MeterPollingService> logger,
        Exception ex,
        string type,
        string name
    )
    {
        if (logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(ex, "Failed to produce reading {Type}:{Name} to Kafka", type, name);
        }
    }

    internal static void CycleComplete(
        this ILogger<MeterPollingService> logger,
        int published,
        int failed,
        int total
    )
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Cycle complete. Published: {Published}, Failed: {Failed}, Total: {Total}",
                published,
                failed,
                total
            );
        }
    }
}
