namespace GraphQLGatewayService.Domain.Contracts;

/// <summary>
/// One location's aggregated series, ordered by period. Grouped server-side because a chart draws
/// one line per location.
/// </summary>
public sealed class AggregateSeries
{
    public string Location { get; init; } = string.Empty;

    /// <summary>The unit the values are measured in, so a chart can label its own axis.</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>The periods, oldest first.</summary>
    public IReadOnlyList<AggregatePoint> Points { get; init; } = [];
}
