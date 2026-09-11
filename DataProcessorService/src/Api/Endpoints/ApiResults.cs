namespace DataProcessorService.Api.Endpoints;

using DataProcessorService.Domain.Common;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Translates a <see cref="Result"/> into an HTTP response, so that endpoints stay free of status
/// code decisions and every failure category maps the same way everywhere.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Maps error categories onto status codes; wiring rather than behaviour.")]
public static class ApiResults
{
    /// <summary>Returns the value on success, or a problem response describing the failure.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="result">The outcome to translate.</param>
    /// <returns>An HTTP result.</returns>
    public static IResult ToHttpResult<TValue>(this Result<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    /// <summary>
    /// Returns a 400 problem response describing an invalid request, matching the shape produced
    /// for validation failures raised inside a handler.
    /// </summary>
    /// <param name="detail">What was wrong with the request.</param>
    /// <returns>An HTTP result.</returns>
    public static IResult ValidationProblem(string detail) =>
        Problem(Error.CreateValidation(detail));

    private static IResult Problem(Error error) =>
        error.Code switch
        {
            ErrorCode.Validation => Results.Problem(
                title: "Invalid request",
                detail: error.Description,
                statusCode: StatusCodes.Status400BadRequest
            ),
            ErrorCode.NotFound => Results.Problem(
                title: "Not found",
                detail: error.Description,
                statusCode: StatusCodes.Status404NotFound
            ),
            // Transient means a dependency is briefly unavailable, so the caller should retry
            // rather than treat the request itself as wrong.
            ErrorCode.Transient => Results.Problem(
                title: "Temporarily unavailable",
                detail: error.Description,
                statusCode: StatusCodes.Status503ServiceUnavailable
            ),
            _ => Results.Problem(
                title: "Request failed",
                detail: error.Description,
                statusCode: StatusCodes.Status500InternalServerError
            ),
        };
}
