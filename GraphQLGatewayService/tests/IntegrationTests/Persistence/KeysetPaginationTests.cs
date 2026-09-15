namespace GraphQLGatewayService.IntegrationTests.Persistence;

using GraphQLGatewayService.Domain.Common;
using GraphQLGatewayService.Domain.Contracts;
using GraphQLGatewayService.Domain.Enums;
using GraphQLGatewayService.Infrastructure.Persistence;
using GraphQLGatewayService.Infrastructure.Persistence.Queries;
using GraphQLGatewayService.Infrastructure.Persistence.ReadModels;
using GreenDonut.Data;

/// <summary>
/// Covers the paging the readings feed depends on. The reason it is keyset rather than offset is
/// that readings arrive continuously, so these tests page across an active insert.
/// </summary>
[Trait("Category", "Integration")]
public sealed class KeysetPaginationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Start = UtcInstant.Normalize(
        new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)
    );

    [Fact]
    public async Task ToPageAsync_ConsecutivePages_DoNotOverlap()
    {
        await this.SeedAsync(count: 12);

        var first = await this.PageAsync(first: 5);
        var second = await this.PageAsync(first: 5, after: Cursor(first));

        Assert.Empty(Ids(first).Intersect(Ids(second)));
    }

    [Fact]
    public async Task ToPageAsync_WalkingEveryPage_VisitsEachRowExactlyOnce()
    {
        await this.SeedAsync(count: 12);

        var seen = new List<long>();
        string? cursor = null;
        for (var page = 0; page < 4; page++)
        {
            var current = await this.PageAsync(first: 5, after: cursor);
            seen.AddRange(Ids(current));
            if (!current.HasNextPage)
            {
                break;
            }

            cursor = Cursor(current);
        }

        Assert.Equal(12, seen.Count);
        Assert.Equal(12, seen.Distinct().Count());
    }

    [Fact]
    public async Task ToPageAsync_AcrossPages_OrdersByCollectedAtThenIdDescending()
    {
        await this.SeedAsync(count: 12);

        var first = await this.PageAsync(first: 5);
        var second = await this.PageAsync(first: 5, after: Cursor(first));

        var keys = first
            .Items.Concat(second.Items)
            .Select(reading => (reading.CollectedAt, reading.Id))
            .ToList();

        Assert.Equal(keys.OrderByDescending(key => key.CollectedAt).ThenByDescending(key => key.Id), keys);
    }

    [Fact]
    public async Task ToPageAsync_RowsSharingOneInstant_PageDeterministically()
    {
        // One poll stamps every sensor with the same collection instant, and the unique index on
        // (sensor_id, collected_at) means that is the only way rows can share one. Without the id
        // tiebreak the boundary between pages could fall anywhere inside that group.
        await fixture.ResetAsync();
        await fixture.SeedAsync(
            [
                .. Enumerable
                    .Range(0, 10)
                    .Select(index =>
                        Energy(
                            PostgresFixture.Sensor($"Room {index}", SensorType.Energy),
                            Start,
                            index
                        )
                    ),
            ]
        );

        var first = await this.PageAsync(first: 4);
        var second = await this.PageAsync(first: 4, after: Cursor(first));

        Assert.Empty(Ids(first).Intersect(Ids(second)));
        Assert.Equal(Ids(first).OrderByDescending(id => id), Ids(first));
        Assert.True(Ids(first).Min() > Ids(second).Max());
    }

    [Fact]
    public async Task ToPageAsync_RowsInsertedAtTheHeadBetweenPages_DoNotShiftTheNextPage()
    {
        // The defect offset paging would have: new rows arriving at the head push everything down,
        // so page two repeats what page one already returned.
        await this.SeedAsync(count: 12);
        var sensor = PostgresFixture.Sensor("Later", SensorType.Energy);

        var first = await this.PageAsync(first: 5);
        var cursor = Cursor(first);

        await fixture.SeedAsync(
            [.. Enumerable.Range(0, 5).Select(index => Energy(sensor, Start.AddDays(1).AddMinutes(index), index))]
        );

        var second = await this.PageAsync(first: 5, after: cursor);

        Assert.Empty(Ids(first).Intersect(Ids(second)));
        Assert.DoesNotContain(second.Items, reading => reading.Sensor.Name == "Later");
    }

    [Fact]
    public async Task ToPageAsync_TotalCountRequested_CountsEveryMatchingRow()
    {
        await this.SeedAsync(count: 12);

        var page = await this.PageAsync(first: 5, includeTotalCount: true);

        Assert.Equal(12, page.TotalCount);
    }

    [Fact]
    public async Task ToPageAsync_FilteredFeed_CountsOnlyMatchingRows()
    {
        await fixture.ResetAsync();
        await fixture.SeedAsync(
            Energy(PostgresFixture.Sensor("Kitchen", SensorType.Energy), Start, 1),
            Energy(PostgresFixture.Sensor("Garage", SensorType.Energy), Start.AddMinutes(1), 2)
        );

        var page = await this.PageAsync(
            first: 5,
            includeTotalCount: true,
            filter: new ReadingFilter { Location = "Garage" }
        );

        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task ToPageAsync_LastPage_ReportsNoNextPage()
    {
        await this.SeedAsync(count: 3);

        var page = await this.PageAsync(first: 10);

        Assert.False(page.HasNextPage);
        Assert.Equal(3, page.Items.Length);
    }

    private static EnergyReadingRow Energy(SensorRow sensor, DateTimeOffset at, double kwh) =>
        new()
        {
            Sensor = sensor,
            CollectedAt = UtcInstant.Normalize(at),
            EnergyKwh = kwh,
        };

    private static List<long> Ids(Page<Reading> page) => [.. page.Items.Select(reading => reading.Id)];

    /// <summary>The cursor a client would send as <c>after</c> to fetch the next page.</summary>
    private static string Cursor(Page<Reading> page) => page.CreateCursor(page.Last!.Value);

    private async Task SeedAsync(int count)
    {
        await fixture.ResetAsync();
        var sensor = PostgresFixture.Sensor("Kitchen", SensorType.Energy);
        await fixture.SeedAsync(
            [.. Enumerable.Range(0, count).Select(index => Energy(sensor, Start.AddMinutes(index), index))]
        );
    }

    private async Task<Page<Reading>> PageAsync(
        int first,
        string? after = null,
        bool includeTotalCount = false,
        ReadingFilter? filter = null
    )
    {
        await using MeterReadingsDbContext context = fixture.CreateContext();

        // Mirrors the resolver exactly: project, then order, then page.
        return await context
            .FilterReadings(filter)
            .ProjectToReading()
            .OrderByDescending(reading => reading.CollectedAt)
            .ThenByDescending(reading => reading.Id)
            .ToPageAsync(
                new PagingArguments(first, after, includeTotalCount: includeTotalCount),
                TestContext.Current.CancellationToken
            );
    }
}
