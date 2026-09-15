namespace GraphQLGatewayService.Domain.Contracts;

using GraphQLGatewayService.Domain.Enums;

/// <summary>
/// Narrows which readings a query returns. Every member is optional, so a client can hold one
/// filter object and send it unchanged.
/// </summary>
public sealed class ReadingFilter
{
    public string? Location { get; set; }

    public SensorType? SensorType { get; set; }

    /// <summary>Inclusive lower bound on collection time.</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>Exclusive upper bound on collection time.</summary>
    public DateTimeOffset? To { get; set; }
}
