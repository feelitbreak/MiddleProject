namespace DataInjectorService.Configuration;

using Confluent.Kafka;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// Strongly-typed configuration for the Kafka producer.
/// Bound from the "Kafka" section in appsettings / environment variables.
/// </summary>
/// <remarks>
/// Not excluded from code coverage, unlike <see cref="WeakAppOptions"/>: <see cref="Validate"/>
/// carries real logic and is covered by unit tests.
/// </remarks>
public sealed class KafkaOptions : IValidatableObject
{
    public const string SectionName = "Kafka";

    /// <summary>Gets or sets the Kafka bootstrap servers (comma-separated).</summary>
    [Required(AllowEmptyStrings = false)]
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>Gets or sets the topic to which meter readings are published.</summary>
    [Required(AllowEmptyStrings = false)]
    public string MeterReadingsTopic { get; set; } = "meter-readings";

    /// <summary>Acknowledgements required before a produce succeeds. Bound case-insensitively.</summary>
    public Acks Acks { get; set; } = Acks.All;

    /// <summary>
    /// Readings are small, repetitive JSON, so compression cuts both broker storage and
    /// consumer fetch volume substantially.
    /// </summary>
    public CompressionType CompressionType { get; set; } = CompressionType.Zstd;

    /// <summary>Capped at 5 while <see cref="EnableIdempotence"/> is on, per <see cref="Validate"/>.</summary>
    [Range(1, 1_000_000)]
    public int MaxInFlightRequestsPerConnection { get; set; } = 5;

    /// <summary>
    /// Exactly-once delivery to a partition, preserving per-key ordering across retries.
    /// Requires <see cref="Acks.All"/> and at most 5 in-flight requests.
    /// </summary>
    public bool EnableIdempotence { get; set; } = true;

    /// <summary>Gets or sets how many times a failed produce request is retried.</summary>
    [Range(0, int.MaxValue)]
    public int MessageSendMaxRetries { get; set; } = 3;

    /// <summary>Gets or sets the backoff, in milliseconds, between produce retries.</summary>
    [Range(1, 300_000)]
    public int RetryBackoffMs { get; set; } = 500;

    /// <summary>Total time a message may spend being produced, retries included.</summary>
    [Range(1, 900_000)]
    public int MessageTimeoutMs { get; set; } = 30_000;

    /// <summary>Gets or sets how many readings one poll publishes at a time.</summary>
    [Range(1, 1_000)]
    public int MaxConcurrentPublishes { get; set; } = 8;

    /// <summary>
    /// Rejects the combinations librdkafka refuses at construction, so misconfiguration fails
    /// at startup with a clear message rather than an opaque broker error.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!this.EnableIdempotence)
        {
            yield break;
        }

        if (this.MaxInFlightRequestsPerConnection > 5)
        {
            yield return new(
                "The idempotent producer supports at most 5 in-flight requests per connection.",
                [nameof(this.MaxInFlightRequestsPerConnection)]
            );
        }

        if (this.Acks != Acks.All)
        {
            yield return new(
                "The idempotent producer requires Acks to be All.",
                [nameof(this.Acks)]
            );
        }
    }
}
