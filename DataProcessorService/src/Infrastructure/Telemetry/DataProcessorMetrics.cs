namespace DataProcessorService.Infrastructure.Telemetry;

using System.Diagnostics.Metrics;

/// <summary>
/// Custom meter and instruments for DataProcessorService business metrics. Registered as a
/// singleton so the consumer and the persistence layer share the same instrument instances.
/// <para>
/// Instrument names follow the repository-wide
/// <c>&lt;service&gt;.&lt;subsystem&gt;.&lt;noun&gt;</c> convention, which Prometheus renders as
/// <c>data_processor_kafka_messages_consumed_total</c> and friends.
/// </para>
/// </summary>
public sealed class DataProcessorMetrics : IDisposable
{
    public const string MeterName = "DataProcessorService";

    private readonly Meter meter;

    /// <summary>Initializes a new instance of the <see cref="DataProcessorMetrics"/> class.</summary>
    public DataProcessorMetrics()
    {
        this.meter = new Meter(MeterName, "1.0.0");

        this.MessagesConsumed = this.meter.CreateCounter<long>(
            "data_processor.kafka.messages_consumed",
            unit: "{message}",
            description: "Number of messages consumed from the readings topic."
        );

        this.BatchesProcessed = this.meter.CreateCounter<long>(
            "data_processor.kafka.batches_processed",
            unit: "{batch}",
            description: "Number of batches successfully persisted and acknowledged."
        );

        this.BatchRetries = this.meter.CreateCounter<long>(
            "data_processor.kafka.batch_retries",
            unit: "{attempt}",
            description: "Number of batch attempts that failed transiently and were retried."
        );

        this.DeadLetteredMessages = this.meter.CreateCounter<long>(
            "data_processor.kafka.dead_lettered_messages",
            unit: "{message}",
            description: "Number of messages moved to the dead-letter topic, tagged by reason."
        );

        this.ReadingsInserted = this.meter.CreateCounter<long>(
            "data_processor.database.readings_inserted",
            unit: "{reading}",
            description: "Number of readings written to the database."
        );

        this.DuplicatesSkipped = this.meter.CreateCounter<long>(
            "data_processor.database.readings_duplicates_skipped",
            unit: "{reading}",
            description: "Number of readings skipped because they were already stored."
        );

        this.BatchDuration = this.meter.CreateHistogram<double>(
            "data_processor.kafka.batch_duration",
            unit: "s",
            description: "Duration of a full decode, persist and commit cycle for one batch."
        );

        this.DatabaseWriteDuration = this.meter.CreateHistogram<double>(
            "data_processor.database.write_duration",
            unit: "s",
            description: "Duration of the ingestion statement for one batch."
        );

        this.ConsumerLag = this.meter.CreateHistogram<double>(
            "data_processor.kafka.consumer_lag",
            unit: "s",
            description: "Age of the newest message in a batch, measured from its collection time."
        );
    }

    public Counter<long> MessagesConsumed { get; }

    public Counter<long> BatchesProcessed { get; }

    public Counter<long> BatchRetries { get; }

    public Counter<long> DeadLetteredMessages { get; }

    public Counter<long> ReadingsInserted { get; }

    /// <summary>Number of readings skipped because they were already stored.</summary>
    public Counter<long> DuplicatesSkipped { get; }

    public Histogram<double> BatchDuration { get; }

    public Histogram<double> DatabaseWriteDuration { get; }

    /// <summary>
    /// End-to-end lag, in seconds, between a reading being collected upstream and being persisted
    /// here. The single most useful number on the dashboard, and free to compute from data the
    /// message already carries.
    /// </summary>
    public Histogram<double> ConsumerLag { get; }

    /// <inheritdoc/>
    public void Dispose() => this.meter.Dispose();
}
