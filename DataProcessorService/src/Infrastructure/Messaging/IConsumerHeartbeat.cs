namespace DataProcessorService.Infrastructure.Messaging;

/// <summary>What the liveness and assignment probes read from the consumer loop.</summary>
public interface IConsumerHeartbeat
{
    DateTimeOffset LastIterationUtc { get; }

    bool HasAssignment { get; }

    void Beat();

    void SetAssignment(bool assigned);
}
