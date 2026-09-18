import { useEffect, useRef, useState } from 'react';
import { HUB_URL } from '../config';
import type { HubConnection } from '@microsoft/signalr';

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
 *
 * The client is imported dynamically: nothing on first paint needs it, and it is 17 kB that would
 * otherwise sit on the critical path.
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
    let connection: HubConnection | undefined;
    let debounce: ReturnType<typeof setTimeout> | undefined;
    // The import resolves after this effect may already have been torn down.
    let cancelled = false;

    const scheduleRefetch = () => {
      if (debounce !== undefined) clearTimeout(debounce);
      debounce = setTimeout(() => onChangedRef.current(), REFETCH_DEBOUNCE_MS);
    };

    const connect = async () => {
      const { HubConnectionBuilder } = await import('@microsoft/signalr');
      if (cancelled) return;

      const hub = new HubConnectionBuilder().withUrl(HUB_URL).withAutomaticReconnect().build();
      connection = hub;

      hub.on('readingsChanged', (event: ReadingsChanged) => {
        setLastEvent(event);
        setEventsSeen((seen) => seen + 1);
        scheduleRefetch();
      });

      hub.onreconnecting(() => setHubState('reconnecting'));
      hub.onreconnected(() => {
        setHubState('connected');
        scheduleRefetch();
      });
      hub.onclose(() => setHubState('disconnected'));

      try {
        await hub.start();
        setHubState(cancelled ? 'disconnected' : 'connected');
      } catch {
        if (!cancelled) setHubState('disconnected');
      }
    };

    void connect();

    return () => {
      cancelled = true;
      if (debounce !== undefined) clearTimeout(debounce);
      void connection?.stop();
    };
  }, []);

  return { hubState, lastEvent, eventsSeen };
}
