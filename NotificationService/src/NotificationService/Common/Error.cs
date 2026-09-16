namespace NotificationService.Common;

/// <summary>
/// Represents an error that can be carried by a <see cref="Result"/> or <see cref="Result{T}"/>.
/// </summary>
/// <remarks>
/// There is no error code here, unlike the sibling services: this service has exactly one expected
/// failure — a message it cannot decode — and nothing branches on the category. A message that fails
/// to decode is dropped, never retried.
/// </remarks>
public sealed class Error
{
    private Error(string description) => this.Description = description;

    /// <summary>Gets a sentinel representing the absence of an error (success path).</summary>
    public static readonly Error None = new(string.Empty);

    /// <summary>Gets a human-readable description, primarily for logging.</summary>
    public string Description { get; }

    /// <summary>Creates an error carrying the given description.</summary>
    public static Error Create(string description) => new(description);

    /// <summary>Returns a string representation of the error for logging.</summary>
    public override string ToString() => this.Description;
}
