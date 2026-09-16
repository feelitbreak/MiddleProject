namespace DataProcessorService.UnitTests.Configuration;

using DataProcessorService.Infrastructure.Configuration;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// Covers configuration validation, which exists so that misconfiguration fails at start-up with a
/// clear message rather than at the first batch with an opaque one.
/// </summary>
public sealed class KafkaOptionsTests
{
    [Fact]
    public void Validate_DefaultOptions_AreValid() => Assert.Empty(Validate(Valid()));

    [Fact]
    public void Validate_DeadLetterTopicEqualsReadingsTopic_IsRejected()
    {
        // Otherwise a poison message is republished to the very topic it was rejected from, which
        // loops forever.
        var options = Valid();
        options.DeadLetterTopic = options.MeterReadingsTopic;

        Assert.Contains(
            Validate(options),
            result =>
                result.ErrorMessage!.Contains("must differ", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Theory]
    [InlineData("meter-readings")]
    [InlineData("meter-readings-dlq")]
    public void Validate_ReadingsPersistedTopicCollidesWithAnother_IsRejected(string topic)
    {
        // Announcing on either topic feeds the completion signal back in as a reading.
        var options = Valid();
        options.ReadingsPersistedTopic = topic;

        Assert.Contains(
            Validate(options),
            result =>
                result.ErrorMessage!.Contains("must differ", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Validate_MaxRetryDelayBelowBaseDelay_IsRejected()
    {
        var options = Valid();
        options.RetryBaseDelayMs = 5_000;
        options.RetryMaxDelayMs = 1_000;

        Assert.Contains(
            Validate(options),
            result => result.ErrorMessage!.Contains("shorter", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingBootstrapServers_IsRejected(string bootstrapServers)
    {
        var options = Valid();
        options.BootstrapServers = bootstrapServers;

        Assert.NotEmpty(Validate(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10_001)]
    public void Validate_BatchSizeOutOfRange_IsRejected(int maxBatchSize)
    {
        var options = Valid();
        options.MaxBatchSize = maxBatchSize;

        Assert.NotEmpty(Validate(options));
    }

    [Fact]
    public void Validate_ZeroAttempts_IsRejected()
    {
        // Zero attempts would mean a batch is dead-lettered without ever being tried.
        var options = Valid();
        options.MaxBatchAttempts = 0;

        Assert.NotEmpty(Validate(options));
    }

    private static KafkaOptions Valid() =>
        new()
        {
            BootstrapServers = "localhost:9092",
            MeterReadingsTopic = "meter-readings",
            DeadLetterTopic = "meter-readings-dlq",
            ReadingsPersistedTopic = "meter-readings-persisted",
            ConsumerGroupId = "data-processor",
        };

    private static List<ValidationResult> Validate(KafkaOptions options)
    {
        var results = new List<ValidationResult>();

        // validateAllProperties mirrors what ValidateDataAnnotations does at start-up, which is
        // what makes these assertions meaningful rather than a test of the attributes alone.
        Validator.TryValidateObject(
            options,
            new ValidationContext(options),
            results,
            validateAllProperties: true
        );

        return results;
    }
}
