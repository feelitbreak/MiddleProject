namespace DataProcessorService.Infrastructure.Configuration;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Strongly-typed configuration for the Kafka consumer and the two producers.
/// Bound from the "Kafka" section in appsettings / environment variables.
/// </summary>
/// <remarks>
/// Not excluded from code coverage, unlike the plain options types: <see cref="Validate"/> carries
/// real logic and is covered by unit tests.
/// </remarks>
public sealed class KafkaOptions : IValidatableObject
{
    public const string SectionName = "Kafka";

    /// <summary>Gets or sets the Kafka bootstrap servers (comma-separated).</summary>
    [Required(AllowEmptyStrings = false)]
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>Gets or sets the topic readings are consumed from.</summary>
    [Required(AllowEmptyStrings = false)]
    public string MeterReadingsTopic { get; set; } = "meter-readings";

    /// <summary>
    /// Gets or sets the topic messages are moved to when they can never be processed.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string DeadLetterTopic { get; set; } = "meter-readings-dlq";

    /// <summary>
    /// Gets or sets the topic announcing that a batch of readings has been committed.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string ReadingsPersistedTopic { get; set; } = "meter-readings-persisted";

    /// <summary>Gets or sets the consumer group identifier.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ConsumerGroupId { get; set; } = "data-processor";

    /// <summary>Gets or sets the maximum number of messages accumulated before a batch is written.</summary>
    [Range(1, 10_000)]
    public int MaxBatchSize { get; set; } = 500;

    /// <summary>How long to hold a partial batch. Bounds latency when the topic is quiet.</summary>
    [Range(10, 60_000)]
    public int BatchLingerMs { get; set; } = 2_000;

    /// <summary>
    /// Retries before a batch is dead-lettered. Bounded on purpose: retrying forever means one
    /// misclassified permanent failure stops the partition with nothing to alert on.
    /// </summary>
    [Range(1, 100)]
    public int MaxBatchAttempts { get; set; } = 5;

    /// <summary>Gets or sets the initial backoff between batch attempts, in milliseconds.</summary>
    [Range(10, 60_000)]
    public int RetryBaseDelayMs { get; set; } = 500;

    /// <summary>Gets or sets the ceiling on the exponential backoff, in milliseconds.</summary>
    [Range(100, 600_000)]
    public int RetryMaxDelayMs { get; set; } = 30_000;

    /// <summary>Exceeding this has the broker declare us dead and rebalance our partitions away.</summary>
    [Range(10_000, 3_600_000)]
    public int MaxPollIntervalMs { get; set; } = 300_000;

    /// <summary>Gets or sets the consumer session timeout, in milliseconds.</summary>
    [Range(6_000, 3_600_000)]
    public int SessionTimeoutMs { get; set; } = 45_000;

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (this.RetryMaxDelayMs < this.RetryBaseDelayMs)
        {
            yield return new(
                "The maximum retry delay cannot be shorter than the base retry delay.",
                [nameof(this.RetryMaxDelayMs)]
            );
        }

        if (string.Equals(this.DeadLetterTopic, this.MeterReadingsTopic, StringComparison.Ordinal))
        {
            yield return new(
                "The dead-letter topic must differ from the readings topic, otherwise poison "
                    + "messages are republished to the topic they were just rejected from.",
                [nameof(this.DeadLetterTopic)]
            );
        }

        if (
            string.Equals(
                this.ReadingsPersistedTopic,
                this.MeterReadingsTopic,
                StringComparison.Ordinal
            )
            || string.Equals(
                this.ReadingsPersistedTopic,
                this.DeadLetterTopic,
                StringComparison.Ordinal
            )
        )
        {
            yield return new(
                "The readings-persisted topic must differ from the readings and dead-letter "
                    + "topics, otherwise a completion signal is fed back in as a reading.",
                [nameof(this.ReadingsPersistedTopic)]
            );
        }
    }
}
