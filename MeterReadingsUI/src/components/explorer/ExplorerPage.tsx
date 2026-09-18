import { useQuery } from '@apollo/client';
import { useCallback, useMemo, useState } from 'react';
import Window from '../shared/Window';
import FilterPanel from '../shared/FilterPanel';
import { LoadBar, ThresholdStrip } from '../shared/Indicators';
import { EmptyPanel, ErrorPanel } from '../shared/StatePanels';
import { CATALOGUE, READINGS_PAGE } from '../../graphql/documents';
import { formatClock, formatDay } from '../../domain/freshness';
import { isBreached } from '../../domain/thresholds';
import type { UseFilter } from '../../hooks/useFilter';

interface ExplorerPageProps {
  controls: UseFilter;
}

function numberCell(value: number | null, breached: boolean, digits = 0) {
  if (value === null) return <td className="na">&mdash;</td>;
  return <td className={breached ? 'num hot' : 'num'}>{value.toFixed(digits)}</td>;
}

export default function ExplorerPage({ controls }: ExplorerPageProps) {
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
  const oldest = nodes.length > 0 ? (nodes[nodes.length - 1]?.collectedAt ?? null) : null;

  return (
    <main className="explorer">
      <FilterPanel
        controls={controls}
        locations={catalogue.data?.locations ?? []}
        showPageSize
        pageSize={pageSize}
        onPageSize={setPageSize}
      />

      <Window title="MATCHING SET" className="a-summary">
        <div className="sum">
          <span className="k">TOTAL COUNT</span>
          <span className="v num">{connection?.totalCount.toLocaleString() ?? '--'}</span>
          <span className="m">READINGS MATCH THIS FILTER</span>
        </div>
        <div className="sum">
          <span className="k">NEWEST</span>
          <span className="v num">{newest === null ? '--' : formatClock(newest)}</span>
          <span className="m">{newest === null ? 'NOTHING LOADED' : formatDay(newest)}</span>
        </div>
        <div className="sum" style={{ borderBottom: 0 }}>
          <span className="k">OLDEST LOADED</span>
          <span className="v num">{oldest === null ? '--' : formatClock(oldest)}</span>
          <span className="m">{nodes.length} ROWS ON SCREEN</span>
        </div>
      </Window>

      <Window
        title="READINGS"
        className="a-readings"
        tone={error ? 'hot' : 'default'}
        query={`readings(first: ${pageSize}${pageInfo?.endCursor ? `, after: "${pageInfo.endCursor.slice(0, 24)}…"` : ''})`}
      >
        {singleLocation && (
          <div className="scope">
            {filter.location?.toUpperCase()}
            {filter.sensorType && <span>&middot;</span>}
            {filter.sensorType}
            <em>columns collapsed &mdash; one location filtered</em>
          </div>
        )}
        {loading && nodes.length > 0 && <LoadBar />}
        <ThresholdStrip />

        {error ? (
          <ErrorPanel error={error} onRetry={() => void refetch()} />
        ) : nodes.length === 0 ? (
          loading ? (
            <EmptyPanel title="LOADING" detail="Fetching the first page of readings." />
          ) : (
            <EmptyPanel
              title="NO READINGS IN RANGE"
              detail="Nothing matches this filter. Widen the range, or clear the location and sensor type."
              actionLabel="CLEAR ALL"
              onAction={controls.clear}
            />
          )
        ) : (
          <div className="scrolls">
            <table>
              <thead>
                <tr>
                  <th>COLLECTED AT</th>
                  {!singleLocation && <th>LOCATION</th>}
                  {!singleLocation && <th>TYPE</th>}
                  <th>
                    CO2<i>PPM</i>
                  </th>
                  <th>
                    PM2.5<i>UG/M3</i>
                  </th>
                  <th>
                    RH<i>%</i>
                  </th>
                  <th>
                    MOTION<i>STATE</i>
                  </th>
                  <th>
                    ENERGY<i>KWH</i>
                  </th>
                  <th>ID</th>
                </tr>
              </thead>
              <tbody>
                {nodes.map((reading) => (
                  <tr key={reading.id}>
                    <td className="when">
                      {formatClock(reading.collectedAt)}
                      <i>{formatDay(reading.collectedAt)}</i>
                    </td>
                    {!singleLocation && (
                      <td className="loc">{reading.sensor.name.toUpperCase()}</td>
                    )}
                    {!singleLocation && <td className="loc">{reading.sensor.type}</td>}
                    {numberCell(reading.co2, isBreached('co2', reading.co2))}
                    {numberCell(reading.pm25, isBreached('pm25', reading.pm25))}
                    {numberCell(reading.humidity, isBreached('humidity', reading.humidity))}
                    {reading.motionDetected === null ? (
                      <td className="na">&mdash;</td>
                    ) : (
                      <td>{reading.motionDetected ? 'YES' : 'NO'}</td>
                    )}
                    {numberCell(reading.energyKwh, false, 1)}
                    <td className="num" style={{ fontSize: '16px', color: 'var(--ink-soft)' }}>
                      {reading.id}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="pager">
          <span className="c">
            SHOWING {nodes.length} OF {connection?.totalCount.toLocaleString() ?? '--'} &middot;
            ORDER NEWEST FIRST (FIXED SERVER-SIDE)
          </span>
          <button
            type="button"
            className="btn"
            onClick={loadMore}
            disabled={pageInfo?.hasNextPage !== true || loading}
            aria-disabled={pageInfo?.hasNextPage !== true || loading}
          >
            LOAD {pageSize} MORE &#9656;
          </button>
        </div>
      </Window>
    </main>
  );
}
