namespace DataInjectorService.Common;

/// <summary>
/// Represents an error that can be carried by a <see cref="Result"/> or <see cref="Result{T}"/>.
/// </summary>
public sealed class Error
{
    private Error(ErrorCode code, string description, TimeSpan? retryAfter = null)
    {
        this.Code = code;
        this.Description = description;
        this.RetryAfter = retryAfter;
    }

    /// <summary>Gets a sentinel representing the absence of an error (success path).</summary>
    public static readonly Error None = new(ErrorCode.None, string.Empty);

    /// <summary>
    /// Gets a general failure error. Details are logged at the source; callers only need to
    /// know that the operation did not produce a usable result.
    /// </summary>
    public static readonly Error Failed = new(ErrorCode.Failed, "The operation failed.");

    /// <summary>Gets the error category.</summary>
    public ErrorCode Code { get; }

    /// <summary>Gets a human-readable description, primarily for logging.</summary>
    public string Description { get; }

    /// <summary>
    /// Gets the suggested back-off duration for <see cref="ErrorCode.RateLimited"/> errors,
    /// or <see langword="null"/> for all other error codes.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Creates a <see cref="ErrorCode.RateLimited"/> error carrying the back-off duration.
    /// </summary>
    /// <param name="retryAfter">How long callers should wait before the next attempt.</param>
    public static Error CreateRateLimited(TimeSpan retryAfter) =>
        new(ErrorCode.RateLimited, "The external API enforced rate limiting.", retryAfter);

    /// <summary>Returns a string representation of the error for logging.</summary>
    public override string ToString() => $"[{this.Code}] {this.Description}";
}
