import { HubConnectionBuilder, HubConnectionState, type HubConnection } from '@microsoft/signalr';
import { useEffect, useRef, useState } from 'react';

/**
 * The `readingsChanged` payload, camelCase on the wire and pinned by an integration test in
 * NotificationService. `sensorType` arrives in the stored spelling: air_quality, motion, energy.
 */
export interface ReadingsChanged {
  readonly publishedAt: string;
  readonly readingCount: number;
  readonly newestCollectedAt: string;
  readonly sensors: readonly { readonly location: string; readonly sensorType: string }[];
}

export type HubState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

export interface LiveReadings {
  readonly hubState: HubState;
  readonly lastEvent: ReadingsChanged | null;
  readonly eventsSeen: number;
}

const HUB_URL = '/hubs/readings';

/**
 * Events arrive in bursts of one or two per injector poll and delivery is at-least-once, so the
 * refetch is debounced rather than fired per event.
 */
const REFETCH_DEBOUNCE_MS = 400;

/**
 * The only place SignalR touches the app. The hub pushes a signal, never data: on an event the
 * client refetches the gateway, which stays the single source of readings. A reconnect also
 * refetches, because events during the gap are lost -- the consumer reads from Latest with no
 * replay, so one query covers whatever was missed.
 */
export function useLiveReadings(onChanged: () => void): LiveReadings {
  const [hubState, setHubState] = useState<HubState>('connecting');
  const [lastEvent, setLastEvent] = useState<ReadingsChanged | null>(null);
  const [eventsSeen, setEventsSeen] = useState(0);

  // Held in a ref so a new callback identity each render does not tear down the connection.
  const onChangedRef = useRef(onChanged);
  useEffect(() => {
    onChangedRef.current = onChanged;
  }, [onChanged]);

  useEffect(() => {
    const connection: HubConnection = new HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .build();

    let debounce: ReturnType<typeof setTimeout> | undefined;
    const scheduleRefetch = () => {
      if (debounce !== undefined) clearTimeout(debounce);
      debounce = setTimeout(() => onChangedRef.current(), REFETCH_DEBOUNCE_MS);
    };

    connection.on('readingsChanged', (event: ReadingsChanged) => {
      setLastEvent(event);
      setEventsSeen((seen) => seen + 1);
      scheduleRefetch();
    });

    connection.onreconnecting(() => setHubState('reconnecting'));
    connection.onreconnected(() => {
      setHubState('connected');
      scheduleRefetch();
    });
    connection.onclose(() => setHubState('disconnected'));

    connection
      .start()
      .then(() => setHubState('connected'))
      .catch(() => setHubState('disconnected'));

    return () => {
      if (debounce !== undefined) clearTimeout(debounce);
      if (connection.state !== HubConnectionState.Disconnected) void connection.stop();
    };
  }, []);

  return { hubState, lastEvent, eventsSeen };
}
