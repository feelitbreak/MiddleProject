import { jest } from '@jest/globals';
import { act, renderHook } from '@testing-library/react';
import { useNow } from './useNow';

describe('useNow', () => {
  beforeEach(() => jest.useFakeTimers());
  afterEach(() => jest.useRealTimers());

  it('should start at the current instant', () => {
    const before = Date.now();
    const { result } = renderHook(() => useNow());

    expect(result.current).toBeGreaterThanOrEqual(before);
  });

  /** Ages are relative: without a tick a quiet feed would keep claiming it was read seconds ago. */
  it('should advance even while nothing else changes', () => {
    const { result } = renderHook(() => useNow(1000));
    const start = result.current;

    act(() => {
      jest.advanceTimersByTime(3000);
    });

    expect(result.current).toBeGreaterThanOrEqual(start + 3000);
  });

  it('should honour a custom interval', () => {
    const { result } = renderHook(() => useNow(5000));
    const start = result.current;

    act(() => {
      jest.advanceTimersByTime(1000);
    });
    expect(result.current).toBe(start);

    act(() => {
      jest.advanceTimersByTime(4000);
    });
    expect(result.current).toBeGreaterThan(start);
  });

  it('should stop ticking once unmounted', () => {
    const { unmount } = renderHook(() => useNow(1000));

    unmount();

    expect(jest.getTimerCount()).toBe(0);
  });
});
