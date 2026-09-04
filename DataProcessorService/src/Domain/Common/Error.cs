namespace DataProcessorService.Domain.Common;

/// <summary>
/// Represents an error that can be carried by a <see cref="Result"/> or <see cref="Result{T}"/>.
/// </summary>
public sealed class Error
{
    private Error(ErrorCode code, string description)
    {
        this.Code = code;
        this.Description = description;
    }

    /// <summary>Gets a sentinel representing the absence of an error (success path).</summary>
    public static readonly Error None = new(ErrorCode.None, string.Empty);

    /// <summary>Gets the error category.</summary>
    public ErrorCode Code { get; }

    /// <summary>Gets a human-readable description, primarily for logging.</summary>
    public string Description { get; }

    /// <summary>
    /// Gets a value indicating whether retrying the identical operation could succeed. Drives the
    /// consumer's decision between seeking back and dead-lettering.
    /// </summary>
    public bool IsRetryable => this.Code == ErrorCode.Transient;

    /// <summary>Creates a <see cref="ErrorCode.Transient"/> error.</summary>
    /// <param name="description">What failed, for logging.</param>
    public static Error CreateTransient(string description) => new(ErrorCode.Transient, description);

    /// <summary>Creates a <see cref="ErrorCode.Permanent"/> error.</summary>
    /// <param name="description">Why the operation can never succeed, for logging.</param>
    public static Error CreatePermanent(string description) => new(ErrorCode.Permanent, description);

    /// <summary>Creates a <see cref="ErrorCode.Validation"/> error.</summary>
    /// <param name="description">Which argument is invalid and why.</param>
    public static Error CreateValidation(string description) =>
        new(ErrorCode.Validation, description);

    /// <summary>Creates a <see cref="ErrorCode.NotFound"/> error.</summary>
    /// <param name="description">Which entity was not found.</param>
    public static Error CreateNotFound(string description) => new(ErrorCode.NotFound, description);

    /// <summary>Returns a string representation of the error for logging.</summary>
    public override string ToString() => $"[{this.Code}] {this.Description}";
}
