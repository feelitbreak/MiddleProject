namespace DataProcessorService.UnitTests.Readings;

using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Contracts;
using DataProcessorService.Application.Readings.GetReadings;
using DataProcessorService.Domain.Common;
using DataProcessorService.Domain.Enums;
using Moq;

/// <summary>
/// Covers request validation and the translation of a page number into a database offset.
/// </summary>
public sealed class GetReadingsQueryHandlerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleAsync_PageBelowOne_IsRejected(int page)
    {
        var result = await HandleAsync(Query(page: page));

        AssertValidationFailure(result, "page must be 1");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetReadingsQuery.MaxPageSize + 1)]
    public async Task HandleAsync_PageSizeOutOfRange_IsRejected(int pageSize)
    {
        var result = await HandleAsync(Query(pageSize: pageSize));

        AssertValidationFailure(result, "pageSize must be between");
    }

    [Fact]
    public async Task HandleAsync_MaximumPageSize_IsAccepted()
    {
        var result = await HandleAsync(Query(pageSize: GetReadingsQuery.MaxPageSize));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task HandleAsync_RangeInverted_IsRejected()
    {
        var now = DateTimeOffset.UtcNow;
        var result = await HandleAsync(Query(from: now, to: now.AddHours(-1)));

        AssertValidationFailure(result, "from must be earlier than to");
    }

    [Fact]
    public async Task HandleAsync_OnlyOneBoundSupplied_IsAccepted()
    {
        Assert.True((await HandleAsync(Query(from: DateTimeOffset.UtcNow))).IsSuccess);
        Assert.True((await HandleAsync(Query(to: DateTimeOffset.UtcNow))).IsSuccess);
    }

    [Theory]
    [InlineData(1, 50, 0)]
    [InlineData(2, 50, 50)]
    [InlineData(3, 20, 40)]
    public async Task HandleAsync_Page_TranslatesToTheExpectedOffset(
        int page,
        int pageSize,
        int expectedSkip
    )
    {
        var queries = Queries();

        await new GetReadingsQueryHandler(queries.Object).HandleAsync(
            Query(page: page, pageSize: pageSize),
            TestContext.Current.CancellationToken
        );

        queries.Verify(
            q =>
                q.ListAsync(
                    It.IsAny<ReadingFilter>(),
                    expectedSkip,
                    pageSize,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_Always_ReportsThePageItAsked()
    {
        var result = await HandleAsync(Query(page: 3, pageSize: 20), totalCount: 101);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Page);
        Assert.Equal(20, result.Value.PageSize);
        Assert.Equal(101, result.Value.TotalCount);
    }

    [Fact]
    public async Task HandleAsync_BlankLocation_IsTreatedAsNoFilter()
    {
        // Otherwise "?location=" would filter to sensors whose name is the empty string and
        // silently return nothing.
        var queries = Queries();

        await new GetReadingsQueryHandler(queries.Object).HandleAsync(
            Query(location: "   "),
            TestContext.Current.CancellationToken
        );

        queries.Verify(
            q =>
                q.ListAsync(
                    It.Is<ReadingFilter>(filter => filter.Location == null),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_PaddedLocation_IsTrimmed()
    {
        var queries = Queries();

        await new GetReadingsQueryHandler(queries.Object).HandleAsync(
            Query(location: "  Kitchen  "),
            TestContext.Current.CancellationToken
        );

        queries.Verify(
            q =>
                q.ListAsync(
                    It.Is<ReadingFilter>(filter => filter.Location == "Kitchen"),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_NonUtcBounds_AreNormalizedBeforeFiltering()
    {
        var queries = Queries();
        var berlinNoon = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.FromHours(2));

        await new GetReadingsQueryHandler(queries.Object).HandleAsync(
            Query(from: berlinNoon),
            TestContext.Current.CancellationToken
        );

        queries.Verify(
            q =>
                q.ListAsync(
                    It.Is<ReadingFilter>(filter =>
                        filter.From!.Value.Offset == TimeSpan.Zero
                        && filter.From.Value.Hour == 10
                    ),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    private static GetReadingsQuery Query(
        string? location = null,
        SensorType? sensorType = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = GetReadingsQuery.DefaultPageSize
    ) => new(location, sensorType, from, to, page, pageSize);

    private static Mock<IReadingQueries> Queries(int totalCount = 0)
    {
        var queries = new Mock<IReadingQueries>();
        queries
            .Setup(q => q.CountAsync(It.IsAny<ReadingFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(totalCount);
        queries
            .Setup(q =>
                q.ListAsync(
                    It.IsAny<ReadingFilter>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        return queries;
    }

    private static Task<Result<PagedResult<ReadingDto>>> HandleAsync(
        GetReadingsQuery query,
        int totalCount = 0
    ) =>
        new GetReadingsQueryHandler(Queries(totalCount).Object).HandleAsync(
            query,
            TestContext.Current.CancellationToken
        );

    private static void AssertValidationFailure<T>(Result<T> result, string expected)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Contains(expected, result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }
}
