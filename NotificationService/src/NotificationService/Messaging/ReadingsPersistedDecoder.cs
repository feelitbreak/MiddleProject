namespace NotificationService.Messaging;

using NotificationService.Common;
using NotificationService.Contracts;
using System.Text.Json;

/// <summary>
/// Turns raw message bytes into the event the hub broadcasts.
/// <para>
/// Every failure here is permanent — malformed JSON will fail identically however often it is
/// retried — so the consumer logs it and moves on. There is no dead-letter topic: the message is a
/// signal that the next batch reissues within seconds, and republishing it would buy nothing.
/// </para>
/// </summary>
public static class ReadingsPersistedDecoder
{
    /// <summary>Decodes one message body.</summary>
    public static Result<ReadingsPersistedMessage> Decode(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return Result.Failure<ReadingsPersistedMessage>(
                Error.Create("The message body is empty.")
            );
        }

        ReadingsPersistedMessage? message;

        try
        {
            message = JsonSerializer.Deserialize<ReadingsPersistedMessage>(
                value,
                ReadingsPersistedMessage.SerializerOptions
            );
        }
        catch (JsonException ex)
        {
            return Result.Failure<ReadingsPersistedMessage>(
                Error.Create($"The message body is not valid JSON: {ex.Message}")
            );
        }

        return message is null
            ? Result.Failure<ReadingsPersistedMessage>(
                Error.Create("The message body deserialized to null.")
            )
            : Result.Success(message);
    }
}
