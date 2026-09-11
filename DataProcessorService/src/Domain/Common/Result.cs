namespace DataProcessorService.Domain.Common;

/// <summary>
/// Represents the outcome of an operation that can either succeed or fail.
/// Mirrors the Result pattern used by DataInjectorService so both services share one error idiom.
/// </summary>
public class Result
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Result"/> class.
    /// </summary>
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new ArgumentException(
                "A successful result cannot carry an error.",
                nameof(error)
            );
        }

        if (!isSuccess && error == Error.None)
        {
            throw new ArgumentException("A failed result must carry an error.", nameof(error));
        }

        this.IsSuccess = isSuccess;
        this.Error = error;
    }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets a value indicating whether the operation failed.</summary>
    public bool IsFailure => !this.IsSuccess;

    /// <summary>Gets the error associated with a failed result, or <see cref="Error.None"/>.</summary>
    public Error Error { get; }

    /// <summary>Creates a successful result.</summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>Creates a successful <see cref="Result{T}"/> carrying <paramref name="value"/>.</summary>
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);

    /// <summary>Creates a failed result carrying the given <paramref name="error"/>.</summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>Creates a failed <see cref="Result{T}"/> carrying the given <paramref name="error"/>.</summary>
    public static Result<T> Failure<T>(Error error) => new(default, false, error);
}

/// <summary>
/// Represents the outcome of an operation that returns a value on success.
/// </summary>
public sealed class Result<T> : Result
{
    private readonly T? value;

    /// <summary>
    /// Initializes a new instance of the <see cref="Result{T}"/> class.
    /// </summary>
    internal Result(T? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        this.value = value;
    }

    /// <summary>
    /// Gets the value carried by a successful result.
    /// </summary>
    public T Value =>
        this.IsSuccess
            ? this.value!
            : throw new InvalidOperationException("Cannot access the value of a failed result.");
}
