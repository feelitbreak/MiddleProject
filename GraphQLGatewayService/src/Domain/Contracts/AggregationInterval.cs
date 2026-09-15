namespace GraphQLGatewayService.Domain.Contracts;

/// <summary>
/// How long one period covers. Each value names a PostgreSQL truncation unit, which is what keeps
/// period boundaries aligned to the calendar rather than to the range's start.
/// </summary>
public enum AggregationInterval
{
    Hour = 1,

    Day = 2,

    Week = 3,

    Month = 4,
}
