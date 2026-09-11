namespace DataProcessorService.Domain.Common;

/// <summary>
/// The category of a failed operation.
/// <para>
/// The <see cref="Transient"/> / <see cref="Permanent"/> split drives message consumption: a
/// transient failure is retried, a permanent one is dead-lettered so a poison message cannot wedge
/// the partition.
/// </para>
/// </summary>
public enum ErrorCode
{
    None,

    /// <summary>A dependency is temporarily unavailable; retrying may succeed.</summary>
    Transient,

    /// <summary>The operation can never succeed as submitted; retrying is pointless.</summary>
    Permanent,

    Validation,

    NotFound,
}
