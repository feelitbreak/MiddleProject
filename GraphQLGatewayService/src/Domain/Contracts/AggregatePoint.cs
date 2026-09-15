namespace GraphQLGatewayService.Domain.Contracts;

/// <summary>
/// One period of an aggregation: every reading collected between <see cref="PeriodStart"/> and the
/// start of the next period.
/// </summary>
public sealed class AggregatePoint
{
    /// <summary>Start of the period, in UTC, aligned to the interval's boundary.</summary>
    public DateTimeOffset PeriodStart { get; init; }

    public int Count { get; init; }

    public double Average { get; init; }

    public double Minimum { get; init; }

    public double Maximum { get; init; }
}
