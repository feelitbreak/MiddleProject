namespace DataInjectorService.Common;

/// <summary>The category of a failed operation carried by a <see cref="Result"/>.</summary>
public enum ErrorCode
{
    None,

    /// <summary>
    /// A general failure: bad response, parse failure, network fault. Details are logged where the
    /// error is produced, so callers need not inspect further.
    /// </summary>
    Failed,

    /// <summary>
    /// The external API rate-limited us. Callers should respect <see cref="Error.RetryAfter"/>.
    /// </summary>
    RateLimited,
}
