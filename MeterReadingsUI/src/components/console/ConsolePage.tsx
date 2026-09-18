import { useQuery } from '@apollo/client';
import { useCallback, useMemo } from 'react';
import Window from '../shared/Window';
import FilterPanel from '../shared/FilterPanel';
import { ErrorPanel } from '../shared/StatePanels';
import LatestValues from './LatestValues';
import SensorTypeAggregates from './SensorTypeAggregates';
import LocationChart from './LocationChart';
import Arrivals from './Arrivals';
import { CATALOGUE, LATEST_READINGS } from '../../graphql/documents';
import { groupByLocation } from '../../domain/locations';
import { useNow } from '../../hooks/useNow';
import type { UseFilter } from '../../hooks/useFilter';
import type { LiveReadings } from '../../hooks/useLiveReadings';

interface ConsolePageProps {
  controls: UseFilter;
  live: LiveReadings;
}

export default function ConsolePage({ controls, live }: ConsolePageProps) {
  const now = useNow();
  const { filter, readingWhere, aggregateWhere } = controls;

  const catalogue = useQuery(CATALOGUE);
  const latest = useQuery(LATEST_READINGS, { variables: { where: readingWhere } });

  const retryLatest = useCallback(() => void latest.refetch(), [latest]);

  const rows = useMemo(
    () => groupByLocation(latest.data?.latestReadings ?? [], now),
    [latest.data, now],
  );

  // Search narrows the locations already on screen; the gateway has no search argument.
  const visible = useMemo(() => {
    const term = filter.search.trim().toLowerCase();
    return term === '' ? rows : rows.filter((row) => row.location.toLowerCase().includes(term));
  }, [rows, filter.search]);

  const locations = catalogue.data?.locations ?? [];
  const sensorCount = catalogue.data?.sensors.length ?? 0;

  return (
    <main className="console">
      <FilterPanel controls={controls} locations={locations} />

      <SensorTypeAggregates rows={visible} />

      {latest.error ? (
        <Window title="LATEST VALUES" tone="hot" className="a-latest">
          <ErrorPanel error={latest.error} onRetry={retryLatest} />
        </Window>
      ) : (
        <LatestValues
          rows={visible}
          sensorCount={sensorCount}
          refreshing={latest.loading && latest.data !== undefined}
          now={now}
          query={`latestReadings(where:) · ${sensorCount} sensors, grouped into ${visible.length} locations`}
        />
      )}

      <Arrivals lastEvent={live.lastEvent} eventsSeen={live.eventsSeen} rows={visible} now={now} />

      <LocationChart metric={filter.metric} interval={filter.interval} where={aggregateWhere} />
    </main>
  );
}
