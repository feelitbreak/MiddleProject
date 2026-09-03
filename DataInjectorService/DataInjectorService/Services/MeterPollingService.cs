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
/// <param name="weakAppService">Service for fetching meter readings.</param>
/// <param name="kafkaProducer">Producer for publishing readings to Kafka.</param>
/// <param name="options">WeakApp configuration options.</param>
/// <param name="logger">Logger instance.</param>
/// <param name="metrics">Business metrics recorder.</param>
public sealed class MeterPollingService(
    IWeakAppService weakAppService,
    IKafkaProducer kafkaProducer,
    IOptions<WeakAppOptions> options,
    ILogger<MeterPollingService> logger,
    DataInjectorMetrics metrics
) : BackgroundService
{
    private readonly WeakAppOptions options = options.Value;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.ServiceStarted(this.options.PollingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextDelay = TimeSpan.FromSeconds(this.options.PollingIntervalSeconds);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await weakAppService.GetMetersAsync(stoppingToken);
                nextDelay = result.IsSuccess
                    ? await this.PublishReadingsAsync(result.Value, stoppingToken)
                    : this.HandleFailure(result.Error, nextDelay);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.UnhandledPollingException(ex);
            }
            finally
            {
                metrics.PollingCycleDuration.Record(stopwatch.Elapsed.TotalSeconds);
            }

            await SafeDelayAsync(nextDelay, stoppingToken);
        }

        logger.ServiceStopped();
    }

    private TimeSpan HandleFailure(Error error, TimeSpan defaultDelay)
    {
        metrics.WeakAppPollFailures.Add(
            1,
            new KeyValuePair<string, object?>("error_code", error.Code.ToString())
        );

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
        var failed = 0;

        foreach (var reading in readings)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await kafkaProducer.ProduceAsync(reading, cancellationToken);
                published++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.ProduceFailed(ex, reading.Type, reading.Name);
                metrics.KafkaMessagesFailed.Add(1);
                failed++;
            }
        }

        metrics.MeterReadingsPolled.Add(readings.Count);
        logger.CycleComplete(published, failed, readings.Count);
        return TimeSpan.FromSeconds(this.options.PollingIntervalSeconds);
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
