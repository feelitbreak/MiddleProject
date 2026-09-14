namespace DataProcessorService.UnitTests.Contracts;

using DataProcessorService.Application.Contracts;

/// <summary>
/// Covers the page arithmetic clients rely on to know when to stop asking for more.
/// </summary>
public sealed class PagedResultTests
{
    [Theory]
    [InlineData(0, 50, 0)]
    [InlineData(1, 50, 1)]
    [InlineData(50, 50, 1)]
    [InlineData(51, 50, 2)]
    [InlineData(100, 50, 2)]
    [InlineData(101, 50, 3)]
    public void TotalPages_RoundsUp(int totalCount, int pageSize, int expected) =>
        Assert.Equal(expected, Page(totalCount, pageSize, page: 1).TotalPages);

    [Fact]
    public void HasMore_OnTheLastPage_IsFalse() =>
        Assert.False(Page(totalCount: 100, pageSize: 50, page: 2).HasMore);

    [Fact]
    public void HasMore_BeforeTheLastPage_IsTrue() =>
        Assert.True(Page(totalCount: 101, pageSize: 50, page: 2).HasMore);

    [Fact]
    public void HasMore_WithNoResults_IsFalse() =>
        Assert.False(Page(totalCount: 0, pageSize: 50, page: 1).HasMore);

    [Fact]
    public void HasMore_PastTheLastPage_IsFalse()
    {
        // Asking for page 9 of 2 is a client mistake, not a reason to claim more pages exist.
        Assert.False(Page(totalCount: 100, pageSize: 50, page: 9).HasMore);
    }

    [Fact]
    public void TotalPages_WithZeroPageSize_DoesNotDivideByZero() =>
        Assert.Equal(0, Page(totalCount: 10, pageSize: 0, page: 1).TotalPages);

    private static PagedResult<string> Page(int totalCount, int pageSize, int page) =>
        new([], page, pageSize, totalCount);
}
