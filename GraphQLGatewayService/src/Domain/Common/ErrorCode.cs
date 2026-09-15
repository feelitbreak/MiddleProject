namespace GraphQLGatewayService.Domain.Common;

/// <summary>
/// The category of a failed operation. Determines the <c>extensions.code</c> on a GraphQL error;
/// see <c>ResultExtensions</c>.
/// </summary>
public enum ErrorCode
{
    None,

    /// <summary>A dependency is temporarily unavailable; retrying may succeed.</summary>
    Transient,

    /// <summary>The operation can never succeed as submitted.</summary>
    Permanent,

    Validation,

    NotFound,
}
