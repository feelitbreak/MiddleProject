namespace GraphQLGatewayService.Api.GraphQL.Queries;

using GraphQLGatewayService.Api.GraphQL.Types;
using HealthCheckService = Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService;

/// <summary>Exposes the service's own health through the schema.</summary>
[QueryType]
internal static partial class HealthGraphQLQueries
{
    /// <summary>
    /// Reports whether the gateway and its database are healthy.
    /// </summary>
    public static async Task<HealthReport> GetHealthAsync(
        HealthCheckService healthChecks,
        CancellationToken cancellationToken
    )
    {
        var report = await healthChecks.CheckHealthAsync(cancellationToken);

        return new HealthReport
        {
            Status = report.Status,
            TotalDurationMs = report.TotalDuration.TotalMilliseconds,
            Checks =
            [
                .. report.Entries.Select(entry => new HealthCheckEntry
                {
                    Name = entry.Key,
                    Status = entry.Value.Status,
                    DurationMs = entry.Value.Duration.TotalMilliseconds,
                }),
            ],
        };
    }
}
