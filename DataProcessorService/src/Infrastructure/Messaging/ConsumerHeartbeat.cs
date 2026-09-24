namespace DataProcessorService.Infrastructure.Messaging;

/// <summary>
/// Records when the consumer loop last completed an iteration, and whether it currently owns any
/// partitions.
/// <para>
/// Exists because "the process is up" is not a useful liveness signal for a consumer: a loop that
/// is deadlocked, or one that has been evicted from its consumer group, keeps the web host serving
/// happily while ingesting nothing. The liveness probe reads this instead.
/// </para>
/// </summary>
public sealed class ConsumerHeartbeat : IConsumerHeartbeat
{
    private long lastIterationTicks = DateTimeOffset.UtcNow.UtcTicks;
    private volatile bool hasAssignment;

    /// <summary>Gets the instant the consumer loop last completed an iteration.</summary>
    public DateTimeOffset LastIterationUtc =>
        new(Interlocked.Read(ref this.lastIterationTicks), TimeSpan.Zero);

    /// <summary>Gets a value indicating whether the consumer currently owns any partitions.</summary>
    public bool HasAssignment => this.hasAssignment;

    /// <summary>Records that the consumer loop completed an iteration.</summary>
    public void Beat() =>
        Interlocked.Exchange(ref this.lastIterationTicks, DateTimeOffset.UtcNow.UtcTicks);

    /// <summary>Records whether the consumer currently owns any partitions.</summary>
    public void SetAssignment(bool assigned) => this.hasAssignment = assigned;
}
