namespace GraphQLGatewayService.Api.GraphQL.Errors;

using GraphQLGatewayService.Infrastructure.Telemetry;
using HotChocolate.Execution;
using HotChocolate.Execution.Instrumentation;

/// <summary>
/// Counts and logs errors refused before execution, which never reach <c>IErrorFilter</c> and
/// would otherwise go unrecorded. Measured gap: cost-analyser rejections reach neither these
/// events nor the filter, so they stay absent from the counter.
/// </summary>
public sealed class GraphQLDiagnosticsListener(
    ILogger<GraphQLDiagnosticsListener> logger,
    GraphQLGatewayMetrics metrics
) : ExecutionDiagnosticEventListener
{
    /// <inheritdoc/>
    public override void ValidationErrors(RequestContext context, IReadOnlyList<IError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        foreach (var error in errors)
        {
            this.Record(error);
        }
    }

    /// <inheritdoc/>
    public override void RequestError(RequestContext context, IError error) => this.Record(error);

    private void Record(IError error)
    {
        metrics.Errors.Add(
            1,
            new KeyValuePair<string, object?>(
                GraphQLGatewayMetrics.ErrorCodeTag,
                error.Code ?? "VALIDATION"
            )
        );

        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                "GraphQL request rejected before execution: [{Code}] {Message}",
                error.Code,
                error.Message
            );
        }
    }
}
