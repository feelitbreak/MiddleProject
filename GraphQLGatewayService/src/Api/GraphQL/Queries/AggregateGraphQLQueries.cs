namespace GraphQLGatewayService.Api.GraphQL.Queries;

using GraphQLGatewayService.Api.GraphQL.Errors;
using GraphQLGatewayService.Domain.Contracts;
using GraphQLGatewayService.Infrastructure.Persistence;
using GraphQLGatewayService.Infrastructure.Persistence.Queries;
using HotChocolate.CostAnalysis.Types;
using System.Globalization;

/// <summary>Time-bucketed aggregation, shaped for charting.</summary>
[QueryType]
internal static partial class AggregateGraphQLQueries
{
    /// <summary>
    /// Aggregates one metric into time periods, one series per location. Only the metric is
    /// required; the interval defaults to hourly and the range to a window suited to it. For
    /// MOTION_DETECTED the average is the fraction with motion. Capped at 90 days, 2000 periods.
    /// </summary>
    // Prices the field for cost analysis; periods per location. The hard bound is
    // AggregateWindow, not this.
    [ListSize(AssumedSize = AggregateWindow.MaxPeriods)]
    public static async Task<IReadOnlyList<AggregateSeries>> GetReadingAggregatesAsync(
        ReadingMetric metric,
        MeterReadingsDbContext context,
        TimeProvider timeProvider,
        IQueryCache cache,
        CancellationToken cancellationToken,
        AggregationInterval interval = AggregationInterval.Hour,
        AggregateFilter? where = null
    )
    {
        ArgumentNullException.ThrowIfNull(cache);

        var window = AggregateWindow
            .Resolve(interval, where?.From, where?.To, timeProvider)
            .ValueOrThrow();

        // Stable for a whole period, because the window sits on period boundaries.
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"aggregates:{metric}:{interval}:{window.From:O}:{window.To:O}:{where?.Location}"
        );

        return await cache.GetOrCreateAsync(
            key,
            (
                Context: context,
                Metric: metric,
                Interval: interval,
                Window: window,
                Location: where?.Location
            ),
            static (state, token) =>
                new ValueTask<IReadOnlyList<AggregateSeries>>(
                    state.Context.AggregateAsync(
                        state.Metric,
                        state.Interval,
                        state.Window,
                        state.Location,
                        token
                    )
                ),
            cache.Aggregates,
            cancellationToken
        );
    }
}
