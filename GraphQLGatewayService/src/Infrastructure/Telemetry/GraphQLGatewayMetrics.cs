namespace GraphQLGatewayService.Infrastructure.Telemetry;

using System.Diagnostics.Metrics;

/// <summary>
/// The service's one custom instrument. Standard ASP.NET Core instrumentation covers request rate
/// and latency, but cannot tell a successful GraphQL operation from a failed one --- the response
/// is HTTP 200 either way --- so the error rate has to be counted from the errors themselves.
/// </summary>
public sealed class GraphQLGatewayMetrics : IDisposable
{
    public const string MeterName = "GraphQLGatewayService";

    /// <summary>Tag carrying the <c>extensions.code</c> reported to the client.</summary>
    public const string ErrorCodeTag = "error_code";

    private readonly Meter meter;

    /// <summary>Initializes a new instance of the <see cref="GraphQLGatewayMetrics"/> class.</summary>
    public GraphQLGatewayMetrics()
    {
        this.meter = new Meter(MeterName, "1.0.0");

        this.Errors = this.meter.CreateCounter<long>(
            "graphql_gateway.graphql.errors",
            unit: "{error}",
            description: "GraphQL errors returned to clients, tagged by error code. Counts errors, "
                + "not responses: one response may carry several."
        );
    }

    public Counter<long> Errors { get; }

    /// <inheritdoc/>
    public void Dispose() => this.meter.Dispose();
}
