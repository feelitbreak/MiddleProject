namespace NotificationService.Configuration;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Strongly-typed configuration for the Kafka consumer.
/// Bound from the "Kafka" section in appsettings / environment variables.
/// </summary>
public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    /// <summary>Gets or sets the Kafka bootstrap servers (comma-separated).</summary>
    [Required(AllowEmptyStrings = false)]
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>Gets or sets the topic announcing that a batch of readings has been committed.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ReadingsPersistedTopic { get; set; } = "meter-readings-persisted";

    /// <summary>
    /// Gets or sets the consumer group identifier. Its own, never the processor's: sharing a group
    /// would have the two services split the partitions and each see half the events.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string ConsumerGroupId { get; set; } = "notification-service";

    /// <summary>Exceeding this has the broker declare us dead and rebalance our partitions away.</summary>
    [Range(10_000, 3_600_000)]
    public int MaxPollIntervalMs { get; set; } = 300_000;

    /// <summary>Gets or sets the consumer session timeout, in milliseconds.</summary>
    [Range(6_000, 3_600_000)]
    public int SessionTimeoutMs { get; set; } = 45_000;
}
