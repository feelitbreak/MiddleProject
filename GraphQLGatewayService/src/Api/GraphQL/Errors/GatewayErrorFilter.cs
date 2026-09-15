namespace GraphQLGatewayService.Api.GraphQL.Errors;

using GraphQLGatewayService.Infrastructure.Telemetry;
using HotChocolate.Execution;

/// <summary>
/// Logs every GraphQL error and keeps internal detail out of the response. Errors carrying an
/// exception are replaced by a generic message plus a correlation id; deliberate ones already have
/// an actionable message and pass through. No environment check: a dev instance is still
/// reachable, and Npgsql messages can carry connection details.
/// </summary>
public sealed class GatewayErrorFilter(
    ILogger<GatewayErrorFilter> logger,
    GraphQLGatewayMetrics metrics
) : IErrorFilter
{
    /// <inheritdoc/>
    public IError OnError(IError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error.Exception is null)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    "GraphQL request rejected at {Path}: [{Code}] {Message}",
                    error.Path?.ToString(),
                    error.Code,
                    error.Message
                );
            }

            metrics.Errors.Add(
                1,
                new KeyValuePair<string, object?>(
                    GraphQLGatewayMetrics.ErrorCodeTag,
                    error.Code ?? "UNKNOWN"
                )
            );

            return error;
        }

        var correlationId = Guid.NewGuid().ToString("N");

        if (logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(
                error.Exception,
                "GraphQL resolver failed at {Path}, correlation id {CorrelationId}",
                error.Path?.ToString(),
                correlationId
            );
        }

        metrics.Errors.Add(
            1,
            new KeyValuePair<string, object?>(
                GraphQLGatewayMetrics.ErrorCodeTag,
                "INTERNAL_SERVER_ERROR"
            )
        );

        // Built from scratch, so nothing the original carried can survive into the response.
        return ErrorBuilder
            .New()
            .SetMessage(
                "Unexpected error. Quote the correlation id when reporting it; the details are in "
                    + "the service log."
            )
            .SetCode("INTERNAL_SERVER_ERROR")
            .SetExtension("correlationId", correlationId)
            .SetPath(error.Path)
            .Build();
    }
}
