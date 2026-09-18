import { useApolloClient, useQuery } from '@apollo/client';
import { useCallback } from 'react';
import { Route, Routes } from 'react-router-dom';
import MenuBar from './components/shared/MenuBar';
import ConsolePage from './components/console/ConsolePage';
import ExplorerPage from './components/explorer/ExplorerPage';
import { GATEWAY_HEALTH } from './graphql/documents';
import { useFilter } from './hooks/useFilter';
import { useLiveReadings } from './hooks/useLiveReadings';
import { useNow } from './hooks/useNow';

const HEALTH_POLL_MS = 30_000;

export default function App() {
  const client = useApolloClient();
  const controls = useFilter();
  const now = useNow();

  // The hub event names what changed but carries no rows, so the response is to reload whatever
  // is on screen and let the gateway stay the single source of readings.
  const onChanged = useCallback(() => {
    void client.refetchQueries({ include: 'active' });
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
      <Routes>
        <Route path="/" element={<ConsolePage controls={controls} live={live} />} />
        <Route path="/readings" element={<ExplorerPage controls={controls} />} />
        <Route path="*" element={<ConsolePage controls={controls} live={live} />} />
      </Routes>
    </>
  );
}
