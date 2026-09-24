import { useApolloClient, useQuery } from '@apollo/client';
import { Suspense, lazy, useCallback } from 'react';
import { Route, Routes } from 'react-router-dom';
import MenuBar from './components/shared/MenuBar';
import ErrorBoundary from './components/shared/ErrorBoundary';
import { EmptyPanel } from './components/shared/StatePanels';
import ConsolePage from './components/console/ConsolePage';
import {
  CATALOGUE,
  GATEWAY_HEALTH,
  LATEST_READINGS,
  READINGS_PAGE,
  READING_AGGREGATES,
} from './graphql/documents';
import { useFilter } from './hooks/useFilter';
import { useLiveReadings } from './hooks/useLiveReadings';
import { useNow } from './hooks/useNow';

// The explorer is a second screen most sessions never open; keeping it out of the initial bundle
// is the cheapest win available on first paint.
const ExplorerPage = lazy(() => import('./components/explorer/ExplorerPage'));

const HEALTH_POLL_MS = 30_000;

// Everything a hub event can invalidate. GATEWAY_HEALTH polls on its own timer.
const LIVE_DOCUMENTS = [CATALOGUE, LATEST_READINGS, READING_AGGREGATES, READINGS_PAGE];

export default function App() {
  const client = useApolloClient();
  const controls = useFilter();
  const now = useNow();

  // The hub event names what changed but carries no rows, so the response is to reload whatever is
  // on screen and let the gateway stay the single source of readings.
  const onChanged = useCallback(() => {
    void client.refetchQueries({ include: LIVE_DOCUMENTS });
  }, [client]);

  const live = useLiveReadings(onChanged);
  const health = useQuery(GATEWAY_HEALTH, { pollInterval: HEALTH_POLL_MS });

  return (
    <>
      <h1 className="sr-only">Meter Readings</h1>
      <MenuBar
        hubState={live.hubState}
        gatewayStatus={health.data?.health.status ?? null}
        clock={new Date(now).toLocaleTimeString([], { hour12: false })}
      />
      <ErrorBoundary>
        <Suspense fallback={<EmptyPanel title="LOADING" detail="Fetching the screen's code." />}>
          <Routes>
            <Route path="/" element={<ConsolePage controls={controls} live={live} />} />
            <Route path="/readings" element={<ExplorerPage controls={controls} />} />
            <Route path="*" element={<ConsolePage controls={controls} live={live} />} />
          </Routes>
        </Suspense>
      </ErrorBoundary>
    </>
  );
}
