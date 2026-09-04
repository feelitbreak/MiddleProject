namespace DataInjectorService.Configuration;

using Confluent.Kafka;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Strongly-typed configuration for the Kafka producer.
/// Bound from the "Kafka" section in appsettings / environment variables.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class KafkaOptions : IValidatableObject
{
    public const string SectionName = "Kafka";

    /// <summary>Gets or sets the Kafka bootstrap servers (comma-separated).</summary>
    [Required(AllowEmptyStrings = false)]
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>Gets or sets the topic to which meter readings are published.</summary>
    [Required(AllowEmptyStrings = false)]
    public string MeterReadingsTopic { get; set; } = "meter-readings";

    /// <summary>
    /// Gets or sets the number of acknowledgements required before a produce call is considered
    /// successful. <see cref="Acks.All"/> waits for all in-sync replicas (safest),
    /// <see cref="Acks.Leader"/> waits for the partition leader only, and
    /// <see cref="Acks.None"/> is fire-and-forget.
    /// <para>
    /// Bound by name and case-insensitively, so "All", "all" and "ALL" are equivalent.
    /// </para>
    /// </summary>
    public Acks Acks { get; set; } = Acks.All;

    /// <summary>
    /// Gets or sets the compression codec applied to produced message batches. Meter readings are
    /// small, highly repetitive JSON documents, so compression materially reduces both broker
    /// storage and consumer fetch volume.
    /// </summary>
    public CompressionType CompressionType { get; set; } = CompressionType.Zstd;

    /// <summary>
    /// Gets or sets the maximum number of in-flight produce requests per connection.
    /// Capped at 5 when <see cref="EnableIdempotence"/> is enabled — see
    /// <see cref="Validate"/>.
    /// </summary>
    [Range(1, 1_000_000)]
    public int MaxInFlightRequestsPerConnection { get; set; } = 5;

    /// <summary>
    /// Gets or sets a value indicating whether the idempotent producer is enabled, which
    /// guarantees exactly-once delivery to a partition and preserves per-key ordering across
    /// retries. Requires <see cref="Acks.All"/> and at most 5 in-flight requests.
    /// </summary>
    public bool EnableIdempotence { get; set; } = true;

    /// <summary>Gets or sets how many times a failed produce request is retried.</summary>
    [Range(0, int.MaxValue)]
    public int MessageSendMaxRetries { get; set; } = 3;

    /// <summary>Gets or sets the backoff, in milliseconds, between produce retries.</summary>
    [Range(1, 300_000)]
    public int RetryBackoffMs { get; set; } = 500;

    /// <summary>
    /// Gets or sets the total time, in milliseconds, a message may spend being produced
    /// (including retries) before it is failed.
    /// </summary>
    [Range(1, 900_000)]
    public int MessageTimeoutMs { get; set; } = 30_000;

    /// <summary>
    /// Validates combinations that librdkafka rejects at producer construction time, so that
    /// misconfiguration surfaces as a clear startup failure rather than an opaque broker error.
    /// </summary>
    /// <param name="validationContext">The validation context (unused).</param>
    /// <returns>One <see cref="ValidationResult"/> per invalid combination.</returns>
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
