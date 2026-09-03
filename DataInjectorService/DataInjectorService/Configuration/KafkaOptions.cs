namespace DataInjectorService.Configuration;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Strongly-typed configuration for the Kafka producer.
/// Bound from the "Kafka" section in appsettings / environment variables.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    /// <summary>Gets or sets the Kafka bootstrap servers (comma-separated).</summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>Gets or sets the topic to which meter readings are published.</summary>
    public string MeterReadingsTopic { get; set; } = "meter-readings";

    /// <summary>
    /// Gets or sets the number of acknowledgements required before a produce call is considered
    /// successful. "All" = wait for all in-sync replicas (safest). "Leader" = wait for partition
    /// leader only. "None" = fire-and-forget.
    /// </summary>
    public string Acks { get; set; } = "All";

    /// <summary>Gets or sets the maximum number of in-flight produce requests per connection.</summary>
    public int MaxInFlightRequestsPerConnection { get; set; } = 5;

    /// <summary>Gets or sets a value indicating whether message ordering is guaranteed (requires MaxInFlight = 1).</summary>
    public bool EnableIdempotence { get; set; } = true;
}
