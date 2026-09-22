import { jest } from '@jest/globals';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReadingsChanged } from './useLiveReadings';

type Handler = (event: ReadingsChanged) => void;

const hub = {
  handlers: new Map<string, Handler>(),
  onReconnecting: undefined as (() => void) | undefined,
  onReconnected: undefined as (() => void) | undefined,
  onClose: undefined as (() => void) | undefined,
  start: jest.fn<() => Promise<void>>(),
  stop: jest.fn<() => Promise<void>>(),
};

function resetHub() {
  hub.handlers.clear();
  hub.onReconnecting = undefined;
  hub.onReconnected = undefined;
  hub.onClose = undefined;
  hub.start.mockReset().mockResolvedValue(undefined);
  hub.stop.mockReset().mockResolvedValue(undefined);
}

jest.unstable_mockModule('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl() {
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    build() {
      return {
        on: (name: string, handler: Handler) => hub.handlers.set(name, handler),
        onreconnecting: (fn: () => void) => (hub.onReconnecting = fn),
        onreconnected: (fn: () => void) => (hub.onReconnected = fn),
        onclose: (fn: () => void) => (hub.onClose = fn),
        start: hub.start,
        stop: hub.stop,
      };
    }
  },
}));

const { useLiveReadings } = await import('./useLiveReadings');

const EVENT: ReadingsChanged = {
  publishedAt: '2026-09-17T12:00:00.000Z',
  readingCount: 18,
  newestCollectedAt: '2026-09-17T11:59:58.000Z',
  sensors: [{ location: 'Kitchen', sensorType: 'air_quality' }],
};

/** Delivers an event the way the hub would, then lets the debounce expire. */
async function emit(event: ReadingsChanged = EVENT) {
  await act(async () => {
    hub.handlers.get('readingsChanged')?.(event);
    await Promise.resolve();
  });
}

async function runDebounce() {
  await act(async () => {
    jest.advanceTimersByTime(500);
    await Promise.resolve();
  });
}

describe('useLiveReadings', () => {
  beforeEach(() => {
    resetHub();
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('should report connected once the hub starts', async () => {
    const { result } = renderHook(() => useLiveReadings(jest.fn()));

    await waitFor(() => expect(result.current.hubState).toBe('connected'));
  });

  it('should report disconnected when the hub cannot start', async () => {
    hub.start.mockRejectedValue(new Error('refused'));

    const { result } = renderHook(() => useLiveReadings(jest.fn()));

    await waitFor(() => expect(result.current.hubState).toBe('disconnected'));
  });

  it('should keep the last event and count what it has seen', async () => {
    const { result } = renderHook(() => useLiveReadings(jest.fn()));
    await waitFor(() => expect(result.current.hubState).toBe('connected'));

    await emit();

    expect(result.current.lastEvent).toEqual(EVENT);
    expect(result.current.eventsSeen).toBe(1);
  });

  /** Delivery is at-least-once across three partitions, so a burst must cost one query, not three. */
  it('should coalesce a burst of events into a single refetch', async () => {
    const onChanged = jest.fn();
    const { result } = renderHook(() => useLiveReadings(onChanged));
    await waitFor(() => expect(result.current.hubState).toBe('connected'));

    await emit();
    await emit();
    await emit();
    expect(onChanged).not.toHaveBeenCalled();

    await runDebounce();

    expect(onChanged).toHaveBeenCalledTimes(1);
    expect(result.current.eventsSeen).toBe(3);
  });

  it('should refetch again for an event that arrives after the debounce has elapsed', async () => {
    const onChanged = jest.fn();
    const { result } = renderHook(() => useLiveReadings(onChanged));
    await waitFor(() => expect(result.current.hubState).toBe('connected'));

    await emit();
    await runDebounce();
    await emit();
    await runDebounce();

    expect(onChanged).toHaveBeenCalledTimes(2);
  });

  /** Events during a disconnect are gone for good, so one query covers whatever was missed. */
  it('should refetch on reconnect to cover the gap', async () => {
    const onChanged = jest.fn();
    const { result } = renderHook(() => useLiveReadings(onChanged));
    await waitFor(() => expect(result.current.hubState).toBe('connected'));

    act(() => hub.onReconnecting?.());
    expect(result.current.hubState).toBe('reconnecting');

    act(() => hub.onReconnected?.());
    await runDebounce();

    expect(result.current.hubState).toBe('connected');
    expect(onChanged).toHaveBeenCalledTimes(1);
  });

  it('should report disconnected when the hub closes', async () => {
    const { result } = renderHook(() => useLiveReadings(jest.fn()));
    await waitFor(() => expect(result.current.hubState).toBe('connected'));

    act(() => hub.onClose?.());

    expect(result.current.hubState).toBe('disconnected');
  });

  it('should use the latest callback without tearing down the connection', async () => {
    const first = jest.fn();
    const second = jest.fn();
    const { result, rerender } = renderHook(({ cb }) => useLiveReadings(cb), {
      initialProps: { cb: first },
    });
    await waitFor(() => expect(result.current.hubState).toBe('connected'));

    rerender({ cb: second });
    await emit();
    await runDebounce();

    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledTimes(1);
    expect(hub.stop).not.toHaveBeenCalled();
  });

  it('should stop the connection when the component goes away', async () => {
    const { result, unmount } = renderHook(() => useLiveReadings(jest.fn()));
    await waitFor(() => expect(result.current.hubState).toBe('connected'));

    unmount();

    expect(hub.stop).toHaveBeenCalledTimes(1);
  });

  it('should not refetch after unmount when a debounce was already pending', async () => {
    const onChanged = jest.fn();
    const { result, unmount } = renderHook(() => useLiveReadings(onChanged));
    await waitFor(() => expect(result.current.hubState).toBe('connected'));

    await emit();
    unmount();
    await runDebounce();

    expect(onChanged).not.toHaveBeenCalled();
  });
});
