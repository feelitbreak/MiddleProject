namespace DataInjectorService.Common;

/// <summary>
/// Identifies the category of a failed operation carried by a <see cref="Result"/>.
/// </summary>
public enum ErrorCode
{
    /// <summary>No error — used only on the success path via <see cref="Error.None"/>.</summary>
    None,

    /// <summary>
    /// The operation failed for a general reason (bad response, parse failure, network fault, etc.).
    /// The details are logged by the service that produced this error; callers need not inspect further.
    /// </summary>
    Failed,

    /// <summary>
    /// The external API enforced rate limiting. Callers should respect
    /// <see cref="Error.RetryAfter"/> before the next attempt.
    /// </summary>
    RateLimited,
}
