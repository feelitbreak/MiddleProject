namespace DataProcessorService.UnitTests.Readings;

using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Contracts;
using DataProcessorService.Application.Readings.GetReadingAggregates;
using DataProcessorService.Domain.Common;
using Moq;

/// <summary>
/// Covers the caps on aggregation.
/// <para>
/// Every one of these guards exists because the alternative is an unbounded scan: without them
/// a single request for hourly periods since 1970 walks the whole table and returns hundreds of
/// thousands of rows.
/// </para>
/// </summary>
public sealed class GetReadingAggregatesQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_NoRangeSupplied_DefaultsToAWindowEndingNow()
    {
        var queries = Queries();

        await HandlerFor(queries)
            .HandleAsync(Query(), TestContext.Current.CancellationToken);

        queries.Verify(
            q =>
                q.AggregateAsync(
                    It.Is<AggregateFilter>(filter =>
                        filter.To == Now && filter.From == Now.AddDays(-1)
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Theory]
    [InlineData(AggregationInterval.Hour, 1)]
    [InlineData(AggregationInterval.Day, 30)]
    [InlineData(AggregationInterval.Week, 90)]
    public async Task HandleAsync_NoRangeSupplied_WindowSuitsTheInterval(
        AggregationInterval interval,
        int expectedDays
    )
    {
        var queries = Queries();

        await HandlerFor(queries)
            .HandleAsync(Query(interval: interval), TestContext.Current.CancellationToken);

        queries.Verify(
            q =>
                q.AggregateAsync(
                    It.Is<AggregateFilter>(filter =>
                        filter.From == Now.AddDays(-expectedDays)
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_RangeInverted_IsRejected()
    {
        var result = await HandleAsync(Query(from: Now, to: Now.AddHours(-1)));

        AssertValidationFailure(result, "from must be earlier than to");
    }

    [Fact]
    public async Task HandleAsync_RangeWiderThanTheCap_IsRejected()
    {
        var result = await HandleAsync(
            Query(
                interval: AggregationInterval.Month,
                from: Now.AddDays(-200),
                to: Now
            )
        );

        AssertValidationFailure(result, "at most 90");
    }

    [Fact]
    public async Task HandleAsync_RangeAtTheCap_IsAccepted()
    {
        var result = await HandleAsync(
            Query(
                interval: AggregationInterval.Day,
                from: Now - GetReadingAggregatesQuery.MaxRange,
                to: Now
            )
        );

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task HandleAsync_TooManyPeriods_IsRejectedAndSuggestsTheFix()
    {
        // Hourly over the full 90 days is about 2160 periods, above the 2000 cap. This is the
        // combination that proves the period cap is reachable rather than hidden behind the range
        // cap, which is what made the original 5000 dead code.
        var result = await HandleAsync(
            Query(
                interval: AggregationInterval.Hour,
                from: Now.AddDays(-89),
                to: Now
            )
        );

        AssertValidationFailure(result, "periods");
        Assert.Contains(
            "Widen the interval",
            result.Error.Description,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task HandleAsync_SameRangeWithAWiderInterval_IsAccepted()
    {
        var result = await HandleAsync(
            Query(interval: AggregationInterval.Day, from: Now.AddDays(-89), to: Now)
        );

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task HandleAsync_BlankLocation_IsTreatedAsNoFilter()
    {
        var queries = Queries();

        await HandlerFor(queries)
            .HandleAsync(Query(location: "  "), TestContext.Current.CancellationToken);

        queries.Verify(
            q =>
                q.AggregateAsync(
                    It.Is<AggregateFilter>(filter => filter.Location == null),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    private static GetReadingAggregatesQuery Query(
        ReadingMetric metric = ReadingMetric.EnergyKwh,
        AggregationInterval interval = AggregationInterval.Hour,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? location = null
    ) => new(metric, interval, from, to, location);

    private static Mock<IReadingQueries> Queries()
    {
        var queries = new Mock<IReadingQueries>();
        queries
            .Setup(q =>
                q.AggregateAsync(It.IsAny<AggregateFilter>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([]);

        return queries;
    }

    private static GetReadingAggregatesQueryHandler HandlerFor(Mock<IReadingQueries> queries) =>
        new(queries.Object, new FixedTimeProvider(Now));

    private static Task<Result<IReadOnlyList<AggregatePeriodDto>>> HandleAsync(
        GetReadingAggregatesQuery query
    ) => HandlerFor(Queries()).HandleAsync(query, TestContext.Current.CancellationToken);

    private static void AssertValidationFailure<T>(Result<T> result, string expected)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Contains(expected, result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A clock stopped at a known instant, so the defaulted range is assertable.</summary>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
