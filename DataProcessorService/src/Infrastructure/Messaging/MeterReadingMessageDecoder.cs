namespace DataProcessorService.Infrastructure.Messaging;

using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Readings.IngestReadingBatch;
using DataProcessorService.Domain.Common;
using DataProcessorService.Domain.Enums;
using System.Text.Json;

/// <summary>
/// Turns raw message bytes into a reading ready for ingestion.
/// <para>
/// Every failure here is <see cref="ErrorCode.Permanent"/>: malformed JSON, an unrecognised sensor
/// type or a payload missing its fields will fail identically no matter how often it is retried,
/// so the consumer dead-letters it rather than blocking the partition.
/// </para>
/// </summary>
public static class MeterReadingMessageDecoder
{
    /// <summary>Decodes one message body.</summary>
    public static Result<ReadingToIngest> Decode(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return Result.Failure<ReadingToIngest>(
                Error.CreatePermanent("The message body is empty.")
            );
        }

        MeterReadingMessage? message;

        try
        {
            message = JsonSerializer.Deserialize<MeterReadingMessage>(
                value,
                MeterReadingMessage.SerializerOptions
            );
        }
        catch (JsonException ex)
        {
            return Result.Failure<ReadingToIngest>(
                Error.CreatePermanent($"The message body is not valid JSON: {ex.Message}")
            );
        }

        if (message is null)
        {
            return Result.Failure<ReadingToIngest>(
                Error.CreatePermanent("The message body deserialized to null.")
            );
        }

        if (string.IsNullOrWhiteSpace(message.Name))
        {
            return Result.Failure<ReadingToIngest>(
                Error.CreatePermanent("The message has no sensor name.")
            );
        }

        if (!SensorTypeNames.TryFromName(message.Type, out var sensorType))
        {
            return Result.Failure<ReadingToIngest>(
                Error.CreatePermanent($"Unrecognised sensor type '{message.Type}'.")
            );
        }

        var values = ReadValues(sensorType, message.Payload);

        return values.IsFailure
            ? Result.Failure<ReadingToIngest>(values.Error)
            : Result.Success(
                new ReadingToIngest(
                    message.Name,
                    sensorType,
                    message.CollectedAt,
                    values.Value
                )
            );
    }

    private static Result<ReadingValues> ReadValues(SensorType type, JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return Result.Failure<ReadingValues>(
                Error.CreatePermanent($"The payload is {payload.ValueKind}, expected an object.")
            );
        }

        return type switch
        {
            SensorType.AirQuality => ReadAirQuality(payload),
            SensorType.Motion => ReadMotion(payload),
            SensorType.Energy => ReadEnergy(payload),
            _ => Result.Failure<ReadingValues>(
                Error.CreatePermanent($"Unsupported sensor type '{type}'.")
            ),
        };
    }

    private static Result<ReadingValues> ReadAirQuality(JsonElement payload)
    {
        if (
            !TryGetInt32(payload, "co2", out var co2)
            || !TryGetInt32(payload, "pm25", out var pm25)
            || !TryGetInt32(payload, "humidity", out var humidity)
        )
        {
            return Result.Failure<ReadingValues>(
                Error.CreatePermanent(
                    "An air quality payload requires numeric co2, pm25 and humidity fields."
                )
            );
        }

        return Result.Success(ReadingValues.ForAirQuality(co2, pm25, humidity));
    }

    private static Result<ReadingValues> ReadMotion(JsonElement payload)
    {
        if (
            !payload.TryGetProperty("motionDetected", out var motion)
            || (motion.ValueKind != JsonValueKind.True && motion.ValueKind != JsonValueKind.False)
        )
        {
            return Result.Failure<ReadingValues>(
                Error.CreatePermanent("A motion payload requires a boolean motionDetected field.")
            );
        }

        return Result.Success(ReadingValues.ForMotion(motion.GetBoolean()));
    }

    private static Result<ReadingValues> ReadEnergy(JsonElement payload)
    {
        if (
            !payload.TryGetProperty("energy", out var energy)
            || energy.ValueKind != JsonValueKind.Number
            || !energy.TryGetDouble(out var kwh)
        )
        {
            return Result.Failure<ReadingValues>(
                Error.CreatePermanent("An energy payload requires a numeric energy field.")
            );
        }

        return Result.Success(ReadingValues.ForEnergy(kwh));
    }

    private static bool TryGetInt32(JsonElement payload, string propertyName, out int value)
    {
        value = 0;

        return payload.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value);
    }
}
