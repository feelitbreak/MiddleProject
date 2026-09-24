namespace DataInjectorService.Services;

using DataInjectorService.Common;
using DataInjectorService.Configuration;
using DataInjectorService.Models;
using DataInjectorService.Telemetry;
using Microsoft.Extensions.Options;
using System.Diagnostics;

/// <summary>
/// Long-running background service that polls the WeakApp <c>/meters</c> endpoint on a
/// configurable interval and publishes each reading to Kafka.
/// <para>
/// On each cycle the service inspects the <see cref="Result{T}"/> returned by
/// <see cref="IWeakAppService.GetMetersAsync"/>:
/// <list type="bullet">
///   <item>Success — publish all readings to Kafka.</item>
///   <item><see cref="ErrorCode.RateLimited"/> — wait for <see cref="Error.RetryAfter"/> before the next cycle.</item>
///   <item><see cref="ErrorCode.Failed"/> — log and continue on the next regular interval.</item>
/// </list>
/// </para>
/// </summary>
public sealed class MeterPollingService(
    IWeakAppService weakAppService,
    IKafkaProducer kafkaProducer,
    IOptions<WeakAppOptions> options,
    ILogger<MeterPollingService> logger,
    DataInjectorMetrics metrics
) : BackgroundService
{
    public const string ActivitySourceName = DataInjectorMetrics.MeterName;

    /// <summary>One span per poll cycle, so every reading published in it shares a trace id.</summary>
    private static readonly ActivitySource Activities = new(ActivitySourceName);

    /// <summary>How many readings one poll publishes at a time.</summary>
    private const int MaxConcurrentPublishes = 8;

    private readonly WeakAppOptions options = options.Value;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.ServiceStarted(this.options.PollingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextDelay = TimeSpan.FromSeconds(this.options.PollingIntervalSeconds);
            var stopwatch = Stopwatch.StartNew();
            var cancelled = false;

            // Scoped to the cycle rather than the loop body, so the span excludes the delay.
            using (Activities.StartActivity("poll meters"))
            {
                try
                {
                    var result = await weakAppService.GetMetersAsync(stoppingToken);
                    metrics.WeakAppRequests.Add(
                        1,
                        new KeyValuePair<string, object?>(
                            "outcome",
                            result.IsSuccess ? "success" : "failure"
                        )
                    );
                    nextDelay = result.IsSuccess
                        ? await this.PublishReadingsAsync(result.Value, stoppingToken)
                        : this.HandleFailure(result.Error, nextDelay);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    cancelled = true;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.UnhandledPollingException(ex);
                }
                finally
                {
                    metrics.PollingCycleDuration.Record(stopwatch.Elapsed.TotalSeconds);
                }
            }

            if (cancelled)
            {
                break;
            }

            await SafeDelayAsync(nextDelay, stoppingToken);
        }

        logger.ServiceStopped();
    }

    private TimeSpan HandleFailure(Error error, TimeSpan defaultDelay)
    {
        if (error.Code == ErrorCode.RateLimited && error.RetryAfter.HasValue)
        {
            logger.RateLimited(error.RetryAfter.Value.TotalSeconds);
            return error.RetryAfter.Value;
        }

        logger.UnusableData(error.Code, error.Description);
        return defaultDelay;
    }

    private async Task<TimeSpan> PublishReadingsAsync(
        IReadOnlyList<MeterReading> readings,
        CancellationToken cancellationToken
    )
    {
        var published = 0;

        // Every reading in a poll belongs to a distinct sensor, and therefore to a distinct
        // message key, so publishing them concurrently preserves the per-key ordering guarantee
        // the key exists to provide while avoiding one broker round-trip per reading.
        await Parallel.ForEachAsync(
            readings,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxConcurrentPublishes,
                CancellationToken = cancellationToken,
            },
            async (reading, token) =>
            {
                if (await this.TryPublishAsync(reading, token))
                {
                    Interlocked.Increment(ref published);
                }
            }
        );

        metrics.MeterReadingsPolled.Add(readings.Count);
        logger.CycleComplete(published, readings.Count - published, readings.Count);
        return TimeSpan.FromSeconds(this.options.PollingIntervalSeconds);
    }

    private async Task<bool> TryPublishAsync(
        MeterReading reading,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await kafkaProducer.ProduceAsync(reading, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.ProduceFailed(ex, reading.Type, reading.Name);
            metrics.KafkaMessagesFailed.Add(1);
            return false;
        }
    }

    private static async Task SafeDelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected during graceful shutdown — the while-loop condition handles exit.
        }
    }
}
