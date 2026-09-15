namespace GraphQLGatewayService.Api.GraphQL.Errors;

using GraphQLGatewayService.Domain.Common;

/// <summary>Bridges the <see cref="Result"/> idiom onto GraphQL's error channel.</summary>
public static class ResultExtensions
{
    /// <summary>
    /// Returns the value of a successful result, or throws the failure as a GraphQL error. These
    /// are caller mistakes, not outcomes a client branches on, so they belong in the error channel
    /// rather than in a schema union.
    /// </summary>
    public static T ValueOrThrow<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return result.Value;
        }

        throw new GraphQLException(
            ErrorBuilder
                .New()
                .SetMessage(result.Error.Description)
                .SetCode(ToErrorCode(result.Error.Code))
                .Build()
        );
    }

    /// <summary>
    /// Maps an <see cref="ErrorCode"/> onto the <c>extensions.code</c> a client sees. A field error
    /// has no HTTP status, so this is the client's only machine-readable signal; the spellings are
    /// the conventional ones an Apollo error link already understands.
    /// </summary>
    private static string ToErrorCode(ErrorCode code) =>
        code switch
        {
            ErrorCode.Validation => "BAD_USER_INPUT",
            ErrorCode.NotFound => "NOT_FOUND",
            ErrorCode.Transient => "SERVICE_UNAVAILABLE",
            ErrorCode.Permanent => "INTERNAL_SERVER_ERROR",
            ErrorCode.None => "INTERNAL_SERVER_ERROR",
            _ => "INTERNAL_SERVER_ERROR",
        };
}
