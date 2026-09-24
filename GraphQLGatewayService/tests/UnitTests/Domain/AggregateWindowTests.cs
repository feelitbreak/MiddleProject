namespace GraphQLGatewayService.UnitTests.Domain;

using GraphQLGatewayService.Domain.Common;
using GraphQLGatewayService.Domain.Contracts;
using Moq;

/// <summary>
/// Covers the bounds every aggregation is held to. These are the only thing standing between a
/// client and an unbounded scan, so each limit is tested for being reachable, not just present.
/// </summary>
public sealed class AggregateWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(AggregationInterval.Hour, 1, 1)]
    [InlineData(AggregationInterval.Day, 30, 24)]
    [InlineData(AggregationInterval.Week, 90, 24 * 7)]
    [InlineData(AggregationInterval.Month, 90, 24 * 31)]
    public void Resolve_NoBounds_CoversWindowSuitedToInterval(
        AggregationInterval interval,
        int expectedDays,
        int longestPeriodHours
    )
    {
        var result = AggregateWindow.Resolve(interval, from: null, to: null, TimeProviderAt(Now));

        // Widening moves each bound by less than one period, which still tells the defaults apart.
        var requested = Now.AddDays(-expectedDays);
        var period = TimeSpan.FromHours(longestPeriodHours);

        Assert.True(result.IsSuccess);
        Assert.InRange(result.Value.From, requested - period, requested);
        Assert.InRange(result.Value.To, Now, Now + period);
    }

    [Fact]
    public void Resolve_OnlyFromSupplied_EndsAtNow()
    {
        var from = new DateTimeOffset(2026, 5, 1, 6, 0, 0, TimeSpan.Zero);

        var result = AggregateWindow.Resolve(
            AggregationInterval.Hour,
            from,
            to: null,
            TimeProviderAt(Now)
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(from, result.Value.From);
        // Now falls at 12:30, and the upper bound covers the whole hour containing it.
        Assert.Equal(new DateTimeOffset(2026, 5, 1, 13, 0, 0, TimeSpan.Zero), result.Value.To);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Resolve_FromNotBeforeTo_IsRejected(int hoursAfterTo)
    {
        var to = Now;
        var from = to.AddHours(hoursAfterTo);

        var result = AggregateWindow.Resolve(AggregationInterval.Hour, from, to, TimeProviderAt(Now));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
    }

    [Fact]
    public void Resolve_RangeWiderThanNinetyDays_IsRejected()
    {
        // Daily spacing keeps the period count low, so this isolates the range limit from the
        // period limit.
        var result = AggregateWindow.Resolve(
            AggregationInterval.Day,
            Now.AddDays(-100),
            Now,
            TimeProviderAt(Now)
        );

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Contains("90", result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_RangeAtNinetyDayLimit_IsAccepted()
    {
        var result = AggregateWindow.Resolve(
            AggregationInterval.Day,
            Now - AggregateWindow.MaxRange,
            Now,
            TimeProviderAt(Now)
        );

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Resolve_TooManyPeriodsWithinAllowedRange_IsRejected()
    {
        // 84 days of hourly periods is 2016, over the cap, while the range itself is still inside
        // the 90-day limit. This is what proves the period cap is reachable rather than sitting
        // unreachable behind the range cap.
        var result = AggregateWindow.Resolve(
            AggregationInterval.Hour,
            Now.AddDays(-84),
            Now,
            TimeProviderAt(Now)
        );

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Contains(
            AggregateWindow.MaxPeriods.ToString(System.Globalization.CultureInfo.InvariantCulture),
            result.Error.Description,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Resolve_PeriodCountJustUnderCap_IsAccepted()
    {
        var result = AggregateWindow.Resolve(
            AggregationInterval.Hour,
            Now.AddDays(-83),
            Now,
            TimeProviderAt(Now)
        );

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Resolve_NonUtcBounds_AreNormalisedToUtc()
    {
        var offset = TimeSpan.FromHours(3);
        var from = new DateTimeOffset(2026, 5, 1, 6, 0, 0, offset);
        var to = new DateTimeOffset(2026, 5, 1, 9, 0, 0, offset);

        var result = AggregateWindow.Resolve(AggregationInterval.Hour, from, to, TimeProviderAt(Now));

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.Zero, result.Value.From.Offset);
        Assert.Equal(TimeSpan.Zero, result.Value.To.Offset);
        Assert.Equal(from.ToUniversalTime(), result.Value.From);
    }

    [Fact]
    public void Resolve_SubMicrosecondBounds_AreTruncated()
    {
        // timestamptz stores microseconds; a tick that survives here would not survive a round
        // trip through the database.
        var from = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(7);

        var result = AggregateWindow.Resolve(
            AggregationInterval.Hour,
            from,
            from.AddHours(1),
            TimeProviderAt(Now)
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.From.Ticks % 10);
    }

    [Fact]
    public void Resolve_NullTimeProvider_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            AggregateWindow.Resolve(AggregationInterval.Hour, null, null, timeProvider: null!)
        );

    private static TimeProvider TimeProviderAt(DateTimeOffset instant)
    {
        var provider = new Mock<TimeProvider>();
        provider.Setup(p => p.GetUtcNow()).Returns(instant);
        return provider.Object;
    }
}
