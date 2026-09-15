namespace GraphQLGatewayService.Api.GraphQL.Types;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>The outcome of one health check.</summary>
public sealed class HealthCheckEntry
{
    public string Name { get; init; } = string.Empty;

    public HealthStatus Status { get; init; }

    public double DurationMs { get; init; }
}

/// <summary>
/// The service's health, as seen through the schema. Check descriptions and exceptions are omitted
/// because any client can reach this field; the probes report those.
/// </summary>
public sealed class HealthReport
{
    /// <summary>Gets the worst status across every check.</summary>
    public HealthStatus Status { get; init; }

    public double TotalDurationMs { get; init; }

    public IReadOnlyList<HealthCheckEntry> Checks { get; init; } = [];
}
