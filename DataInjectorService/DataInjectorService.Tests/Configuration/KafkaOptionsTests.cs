namespace DataInjectorService.Tests.Configuration;

using Confluent.Kafka;
using DataInjectorService.Configuration;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// Covers the cross-field validation added so that librdkafka's own restrictions surface as clear
/// start-up failures instead of opaque producer-construction errors.
/// <para>
/// Before this validation existed, <c>ValidateDataAnnotations().ValidateOnStart()</c> was called on
/// options carrying no attributes at all, so it silently did nothing.
/// </para>
/// </summary>
public sealed class KafkaOptionsTests
{
    [Fact]
    public void Validate_DefaultOptions_AreValid() => Assert.Empty(Validate(Valid()));

    [Fact]
    public void Validate_IdempotenceWithTooManyInFlightRequests_IsRejected()
    {
        // librdkafka caps in-flight requests at five while the idempotent producer is enabled.
        var options = Valid();
        options.EnableIdempotence = true;
        options.MaxInFlightRequestsPerConnection = 6;

        Assert.Contains(
            Validate(options),
            result =>
                result.ErrorMessage!.Contains("in-flight", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Validate_IdempotenceWithWeakerAcks_IsRejected()
    {
        var options = Valid();
        options.EnableIdempotence = true;
        options.Acks = Acks.Leader;

        Assert.Contains(
            Validate(options),
            result => result.ErrorMessage!.Contains("Acks", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Validate_WeakerAcksWithoutIdempotence_IsAllowed()
    {
        var options = Valid();
        options.EnableIdempotence = false;
        options.Acks = Acks.None;
        options.MaxInFlightRequestsPerConnection = 10;

        Assert.Empty(Validate(options));
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

    [Fact]
    public void Validate_ZeroInFlightRequests_IsRejected()
    {
        var options = Valid();
        options.MaxInFlightRequestsPerConnection = 0;

        Assert.NotEmpty(Validate(options));
    }

    private static KafkaOptions Valid() =>
        new() { BootstrapServers = "localhost:9092", MeterReadingsTopic = "meter-readings" };

    private static List<ValidationResult> Validate(KafkaOptions options)
    {
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            options,
            new ValidationContext(options),
            results,
            validateAllProperties: true
        );

        return results;
    }
}
