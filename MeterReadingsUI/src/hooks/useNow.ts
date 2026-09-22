import { useEffect, useState } from 'react';

/**
 * Ages are relative, so the clock has to advance even when no data arrives: without this a feed
 * that went quiet would keep claiming it was read 14 seconds ago.
 */
export function useNow(intervalMs = 1000): number {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), intervalMs);
    return () => clearInterval(timer);
  }, [intervalMs]);

  return now;
}
