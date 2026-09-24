namespace GraphQLGatewayService.Domain.Contracts;

/// <summary>
/// Narrows which readings an aggregation covers. Deliberately not <see cref="ReadingFilter"/>: the
/// metric already fixes the sensor type, so one here could only contradict it.
/// </summary>
public sealed class AggregateFilter
{
    public string? Location { get; set; }

    /// <summary>Inclusive lower bound, widened back to its period start. Defaults by interval.</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>Exclusive upper bound, widened forward to the next period start. Defaults to now.</summary>
    public DateTimeOffset? To { get; set; }
}
