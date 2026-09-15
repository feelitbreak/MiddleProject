namespace GraphQLGatewayService.Api.HealthChecks;

using HotChocolate.Execution;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Reports whether the GraphQL schema can be built. HotChocolate builds it lazily, so without this
/// a broken schema would pass readiness and surface as a 500 to the first real client.
/// </summary>
public sealed class GraphQLSchemaHealthCheck(IRequestExecutorProvider executorProvider)
    : IHealthCheck
{
    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await executorProvider.GetExecutorAsync(cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy("The GraphQL schema is built.");
        }
        catch (GraphQLException ex)
        {
            return HealthCheckResult.Unhealthy("The GraphQL schema could not be built.", ex);
        }
    }
}
