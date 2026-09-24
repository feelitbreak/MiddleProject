namespace GraphQLGatewayService.UnitTests.Domain;

using GraphQLGatewayService.Domain.Common;

/// <summary>
/// Covers the two things <c>timestamptz</c> requires of an inbound timestamp: a zero offset, and
/// microsecond precision.
/// </summary>
public sealed class UtcInstantTests
{
    [Fact]
    public void Normalize_NonZeroOffset_ConvertsToUtc()
    {
        var value = new DateTimeOffset(2026, 5, 1, 15, 0, 0, TimeSpan.FromHours(3));

        var normalized = UtcInstant.Normalize(value);

        Assert.Equal(TimeSpan.Zero, normalized.Offset);
        Assert.Equal(value.ToUniversalTime(), normalized);
    }

    [Fact]
    public void Normalize_SubMicrosecondTicks_AreTruncated()
    {
        var value = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(17);

        var normalized = UtcInstant.Normalize(value);

        Assert.Equal(0, normalized.Ticks % 10);
        Assert.Equal(7, value.Ticks - normalized.Ticks);
    }

    [Fact]
    public void Normalize_NullValue_ReturnsNull() => Assert.Null(UtcInstant.Normalize(null));

    [Fact]
    public void Normalize_NullableWithOffset_ConvertsToUtc()
    {
        DateTimeOffset? value = new DateTimeOffset(2026, 5, 1, 15, 0, 0, TimeSpan.FromHours(3));

        Assert.Equal(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero), UtcInstant.Normalize(value));
    }

    [Fact]
    public void Normalize_AlreadyNormalized_IsUnchanged()
    {
        var value = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(value, UtcInstant.Normalize(value));
    }
}
