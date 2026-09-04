namespace DataProcessorService.Domain.Common;

/// <summary>
/// Identifies the category of a failed operation carried by a <see cref="Result"/>.
/// <para>
/// The <see cref="Transient"/> / <see cref="Permanent"/> split is load-bearing for message
/// consumption: a transient failure must be retried without advancing the Kafka offset, while a
/// permanent one must be dead-lettered so the partition is never wedged by a poison message.
/// </para>
/// </summary>
public enum ErrorCode
{
    /// <summary>No error --- used only on the success path via <see cref="Error.None"/>.</summary>
    None,

    /// <summary>
    /// A dependency is temporarily unavailable (database unreachable, broker in error, deadlock).
    /// Retrying the identical operation later may succeed.
    /// </summary>
    Transient,

    /// <summary>
    /// The operation can never succeed as submitted (malformed payload, unrecognised sensor type,
    /// value outside the storable range). Retrying is pointless.
    /// </summary>
    Permanent,

    /// <summary>The request was well-formed but its arguments are invalid.</summary>
    Validation,

    /// <summary>The requested entity does not exist.</summary>
    NotFound,
}
