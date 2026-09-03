namespace DataInjectorService.Telemetry;

using System.Diagnostics.Metrics;

/// <summary>
/// Defines the custom <see cref="Meter"/> and instruments for DataInjectorService business
/// metrics (WeakApp polling and Kafka production). Registered as a singleton so the same
/// instrument instances are shared across <c>MeterPollingService</c> and <c>KafkaProducer</c>.
/// </summary>
public sealed class DataInjectorMetrics : IDisposable
{
    /// <summary>Name of the <see cref="Meter"/> registered with OpenTelemetry.</summary>
    public const string MeterName = "DataInjectorService";

    private readonly Meter meter;

    /// <summary>Initializes a new instance of the <see cref="DataInjectorMetrics"/> class.</summary>
    public DataInjectorMetrics()
    {
        this.meter = new Meter(MeterName, "1.0.0");

        this.KafkaMessagesProduced = this.meter.CreateCounter<long>(
            "data_injector.kafka.messages_produced",
            unit: "{message}",
            description: "Number of meter readings successfully published to Kafka."
        );

        this.KafkaMessagesFailed = this.meter.CreateCounter<long>(
            "data_injector.kafka.messages_failed",
            unit: "{message}",
            description: "Number of meter readings that failed to publish to Kafka."
        );

        this.MeterReadingsPolled = this.meter.CreateCounter<long>(
            "data_injector.weakapp.readings_polled",
            unit: "{reading}",
            description: "Number of meter readings fetched from WeakApp."
        );

        this.WeakAppPollFailures = this.meter.CreateCounter<long>(
            "data_injector.weakapp.poll_failures",
            unit: "{failure}",
            description: "Number of failed WeakApp poll attempts, tagged by error code."
        );

        this.PollingCycleDuration = this.meter.CreateHistogram<double>(
            "data_injector.polling.cycle_duration",
            unit: "s",
            description: "Duration of a full MeterPollingService poll+publish cycle."
        );

        this.KafkaProduceDuration = this.meter.CreateHistogram<double>(
            "data_injector.kafka.produce_duration",
            unit: "s",
            description: "Duration of an individual Kafka ProduceAsync call."
        );
    }

    /// <summary>Number of meter readings successfully published to Kafka.</summary>
    public Counter<long> KafkaMessagesProduced { get; }

    /// <summary>Number of meter readings that failed to publish to Kafka.</summary>
    public Counter<long> KafkaMessagesFailed { get; }

    /// <summary>Number of meter readings fetched from WeakApp.</summary>
    public Counter<long> MeterReadingsPolled { get; }

    /// <summary>Number of failed WeakApp poll attempts, tagged by error code.</summary>
    public Counter<long> WeakAppPollFailures { get; }

    /// <summary>Duration, in seconds, of a full poll+publish cycle.</summary>
    public Histogram<double> PollingCycleDuration { get; }

    /// <summary>Duration, in seconds, of an individual Kafka <c>ProduceAsync</c> call.</summary>
    public Histogram<double> KafkaProduceDuration { get; }

    /// <inheritdoc/>
    public void Dispose() => this.meter.Dispose();
}
