import { useQuery } from '@apollo/client';
import { useMemo } from 'react';
import Window from '../shared/Window';
import SteppedChart, { ChartLegend, type ChartSeries } from '../shared/SteppedChart';
import { RefreshBar } from '../shared/Indicators';
import { EmptyPanel, ErrorPanel } from '../shared/StatePanels';
import { READING_AGGREGATES } from '../../graphql/documents';
import { chartThreshold, METRICS, seriesColour } from '../../domain/metrics';
import { formatClock } from '../../domain/freshness';
import type {
  AggregateFilterInput,
  AggregationInterval,
  ReadingMetric,
} from '../../graphql/generated/graphql';

interface LocationChartProps {
  metric: ReadingMetric;
  interval: AggregationInterval;
  where: AggregateFilterInput;
}

const MAX_TICKS = 6;

/**
 * `readingAggregates` already returns one series per location with its own unit, so a chart maps
 * straight onto it: no client-side grouping and no unit lookup to drift from the metric enum.
 */
export default function LocationChart({ metric, interval, where }: LocationChartProps) {
  const { data, loading, error, refetch } = useQuery(READING_AGGREGATES, {
    variables: { metric, interval, where },
  });

  const aggregates = useMemo(() => data?.readingAggregates ?? [], [data]);

  const { series, ticks, unit } = useMemo(() => {
    // Periods are aligned to the interval server-side, so the union of period starts is the axis.
    const allPeriods = [
      ...new Set(aggregates.flatMap((s) => s.points.map((p) => p.periodStart))),
    ].sort();

    const built: ChartSeries[] = aggregates.map((s, index) => {
      const byPeriod = new Map(s.points.map((p) => [p.periodStart, p.average]));
      const values = allPeriods.map((period) => byPeriod.get(period) ?? null);
      const gap = values.indexOf(null);
      return {
        name: s.location,
        colour: seriesColour(index),
        values,
        // The last period that reported, so the hatched gap says since when.
        lostAt: gap > 0 ? formatClock(allPeriods[gap - 1] ?? '').slice(0, 5) : undefined,
      };
    });

    const step = Math.max(1, Math.ceil(allPeriods.length / MAX_TICKS));
    const labels = allPeriods
      .filter((_, i) => i % step === 0)
      .map((period) => formatClock(period).slice(0, 5));

    return {
      series: built,
      ticks: labels.length > 1 ? labels : ['START', 'NOW'],
      unit: aggregates[0]?.unit ?? METRICS.find((m) => m.value === metric)?.unit ?? '',
    };
  }, [aggregates, metric]);

  const label = METRICS.find((m) => m.value === metric)?.label ?? metric;
  const query = `readingAggregates(metric: ${metric}, interval: ${interval}) · unit ${unit}`;

  return (
    <Window
      title="AGGREGATE &mdash; BY LOCATION"
      className="area-aggregate"
      tone={error ? 'error' : 'default'}
      query={query}
    >
      {loading && aggregates.length > 0 && <RefreshBar />}
      {error ? (
        <ErrorPanel error={error} onRetry={() => void refetch()} />
      ) : aggregates.length === 0 ? (
        loading ? (
          <EmptyPanel title="LOADING" detail={`Fetching ${label} aggregates.`} />
        ) : (
          <EmptyPanel
            title="NO PERIODS IN RANGE"
            detail="No readings fall in this window, so there is nothing to aggregate. Widen the range or clear the location filter."
          />
        )
      ) : (
        <>
          <div className="chart-area">
            <SteppedChart
              series={series}
              ticks={ticks}
              unit={unit}
              threshold={chartThreshold(metric)}
            />
          </div>
          <ChartLegend series={series} note={'SERVER-SIDE CAPS · 90 DAYS · 2000 PERIODS'} />
        </>
      )}
    </Window>
  );
}
