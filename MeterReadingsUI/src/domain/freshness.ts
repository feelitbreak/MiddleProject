/**
 * The injector polls WeakApp every 60 seconds, so one missed poll is ordinary and several are not.
 * These bounds are read against a reading's collectedAt; the gateway exposes no status of its own.
 */
const STALE_AFTER_MS = 2 * 60 * 1000;
const LOST_AFTER_MS = 15 * 60 * 1000;

export type FeedState = 'live' | 'stale' | 'lost';

const RANK: Record<FeedState, number> = { live: 0, stale: 1, lost: 2 };

export function feedStateAt(collectedAt: string, now: number): FeedState {
  const age = now - Date.parse(collectedAt);
  if (age >= LOST_AFTER_MS) return 'lost';
  if (age >= STALE_AFTER_MS) return 'stale';
  return 'live';
}

/** Worst of the two, for a location whose sensors disagree. */
export function worseState(a: FeedState, b: FeedState): FeedState {
  return RANK[a] >= RANK[b] ? a : b;
}

export function formatAge(collectedAt: string, now: number): string {
  const seconds = Math.max(0, Math.round((now - Date.parse(collectedAt)) / 1000));
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m`;
  return `${Math.floor(minutes / 60)}h${String(minutes % 60).padStart(2, '0')}`;
}

export function formatClock(iso: string): string {
  return new Date(iso).toLocaleTimeString([], {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  });
}

export function formatDay(iso: string): string {
  return new Date(iso)
    .toLocaleDateString([], { day: '2-digit', month: 'short', year: 'numeric' })
    .toUpperCase();
}
