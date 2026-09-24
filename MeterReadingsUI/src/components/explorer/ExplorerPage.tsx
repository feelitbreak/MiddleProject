import { useQuery } from '@apollo/client';
import { useCallback, useMemo, useState } from 'react';
import Window from '../shared/Window';
import FilterPanel from '../shared/FilterPanel';
import { RefreshBar, ThresholdStrip } from '../shared/Indicators';
import { EmptyPanel, ErrorPanel } from '../shared/StatePanels';
import { CATALOGUE, READINGS_PAGE } from '../../graphql/documents';
import { formatClock, formatDay } from '../../domain/freshness';
import { isBreached } from '../../domain/thresholds';
import type { UseFilter } from '../../hooks/useFilter';

interface ExplorerPageProps {
  controls: UseFilter;
}

function numberCell(value: number | null, breached: boolean, digits = 0) {
  if (value === null) return <td className="no-value">&mdash;</td>;
  return <td className={breached ? 'tabular out-of-band' : 'tabular'}>{value.toFixed(digits)}</td>;
}

export default function ExplorerPage({ controls }: Readonly<ExplorerPageProps>) {
  const [pageSize, setPageSize] = useState(25);
  const { filter, readingWhere } = controls;

  const catalogue = useQuery(CATALOGUE);
  const { data, loading, error, refetch, fetchMore } = useQuery(READINGS_PAGE, {
    variables: { first: pageSize, where: readingWhere },
  });

  const connection = data?.readings;
  const nodes = useMemo(() => connection?.nodes ?? [], [connection]);
  const pageInfo = connection?.pageInfo;

  const loadMore = useCallback(() => {
    if (pageInfo?.endCursor == null) return;
    void fetchMore({ variables: { first: pageSize, after: pageInfo.endCursor } });
  }, [fetchMore, pageInfo, pageSize]);

  // Identity columns collapse into a banner when the filter already fixes them for every row.
  const singleLocation = filter.location !== null;
  const newest = nodes[0]?.collectedAt ?? null;
  const oldest = nodes.at(-1)?.collectedAt ?? null;
  const hasNextPage = pageInfo?.hasNextPage === true;

  const cursorClause =
    pageInfo?.endCursor == null ? '' : `, after: "${pageInfo.endCursor.slice(0, 24)}…"`;
  const readingsQuery = `readings(first: ${pageSize}${cursorClause})`;

  const readingsTable = (
    <div className="scroll-area">
      <table>
        <thead>
          <tr>
            <th>COLLECTED AT</th>
            {!singleLocation && <th>LOCATION</th>}
            {!singleLocation && <th>TYPE</th>}
            <th>
              CO2<i className="column-unit">PPM</i>
            </th>
            <th>
              PM2.5<i className="column-unit">UG/M3</i>
            </th>
            <th>
              RH<i className="column-unit">%</i>
            </th>
            <th>
              MOTION<i className="column-unit">STATE</i>
            </th>
            <th>
              ENERGY<i className="column-unit">KWH</i>
            </th>
            <th>ID</th>
          </tr>
        </thead>
        <tbody>
          {nodes.map((reading) => (
            <tr key={reading.id}>
              <td className="timestamp-cell">
                {formatClock(reading.collectedAt)}
                <i className="timestamp-date">{formatDay(reading.collectedAt)}</i>
              </td>
              {!singleLocation && (
                <td className="location-cell">{reading.sensor.name.toUpperCase()}</td>
              )}
              {!singleLocation && <td className="location-cell">{reading.sensor.type}</td>}
              {numberCell(reading.co2, isBreached('co2', reading.co2))}
              {numberCell(reading.pm25, isBreached('pm25', reading.pm25))}
              {numberCell(reading.humidity, isBreached('humidity', reading.humidity))}
              {reading.motionDetected === null ? (
                <td className="no-value">&mdash;</td>
              ) : (
                <td>{reading.motionDetected ? 'YES' : 'NO'}</td>
              )}
              {numberCell(reading.energyKwh, false, 1)}
              <td className="tabular row-id">{reading.id}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );

  const emptyState = loading ? (
    <EmptyPanel title="LOADING" detail="Fetching the first page of readings." />
  ) : (
    <EmptyPanel
      title="NO READINGS IN RANGE"
      detail="Nothing matches this filter. Widen the range, or clear the location and sensor type."
      actionLabel="CLEAR ALL"
      onAction={controls.clear}
    />
  );

  let body;
  if (error) {
    body = <ErrorPanel error={error} onRetry={() => void refetch()} />;
  } else if (nodes.length === 0) {
    body = emptyState;
  } else {
    body = readingsTable;
  }

  return (
    <main className="explorer">
      <div className="explorer-side">
        <FilterPanel
          controls={controls}
          locations={catalogue.data?.locations ?? []}
          catalogueFailed={catalogue.error !== undefined}
          showPageSize
          pageSize={pageSize}
          onPageSize={setPageSize}
        />

        <Window title="MATCHING SET">
          <div className="summary-item">
            <span className="summary-label">TOTAL COUNT</span>
            <span className="summary-value tabular">
              {connection?.totalCount.toLocaleString() ?? '--'}
            </span>
            <span className="summary-meta">READINGS MATCH THIS FILTER</span>
          </div>
          <div className="summary-item">
            <span className="summary-label">NEWEST</span>
            <span className="summary-value tabular">
              {newest === null ? '--' : formatClock(newest)}
            </span>
            <span className="summary-meta">
              {newest === null ? 'NOTHING LOADED' : formatDay(newest)}
            </span>
          </div>
          <div className="summary-item">
            <span className="summary-label">OLDEST LOADED</span>
            <span className="summary-value tabular">
              {oldest === null ? '--' : formatClock(oldest)}
            </span>
            <span className="summary-meta">{nodes.length} ROWS ON SCREEN</span>
          </div>
        </Window>
      </div>

      <Window
        title="READINGS"
        className="area-readings"
        tone={error ? 'error' : 'default'}
        query={readingsQuery}
      >
        {singleLocation && (
          <div className="scope-banner">
            {filter.location?.toUpperCase()}
            {filter.sensorType && <span className="scope-separator">&middot;</span>}
            {filter.sensorType}
            <em className="scope-note">columns collapsed &mdash; one location filtered</em>
          </div>
        )}
        {loading && nodes.length > 0 && <RefreshBar />}
        <ThresholdStrip />

        {body}

        <div className="pager">
          <span className="pager-note">
            SHOWING {nodes.length} OF {connection?.totalCount.toLocaleString() ?? '--'} &middot;
            ORDER NEWEST FIRST (FIXED SERVER-SIDE)
          </span>
          <button
            type="button"
            className="button"
            onClick={loadMore}
            disabled={!hasNextPage || loading}
            aria-disabled={!hasNextPage || loading}
          >
            LOAD {pageSize} MORE &#9656;
          </button>
        </div>
      </Window>
    </main>
  );
}
