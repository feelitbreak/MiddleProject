import type { FeedState } from '../../domain/freshness';
import type { Breach } from '../../domain/thresholds';
import { ALERT_THRESHOLDS } from '../../domain/thresholds';

const FEED_WORD: Record<FeedState, string> = {
  live: 'LIVE',
  stale: 'STALE',
  lost: 'LOST',
};

const FEED_CLASS: Record<FeedState, string> = {
  live: 'feed-chip-live',
  stale: 'feed-chip-stale',
  lost: 'feed-chip-lost',
};

/** Feed age. Carries its word as well as its fill, so colour is never the only signal. */
export function FeedChip({ state, age }: Readonly<{ state: FeedState; age: string }>) {
  return (
    <span className={`feed-chip ${FEED_CLASS[state]}`}>
      <b className="feed-chip-word">{FEED_WORD[state]}</b>
      <span className="feed-chip-age tabular">{age}</span>
    </span>
  );
}

/** Value band. Separate from feed age: a sensor can be stale and out of band at once. */
export function BandChip({
  breaches,
  hasThreshold,
}: Readonly<{
  breaches: Breach[];
  hasThreshold: boolean;
}>) {
  if (!hasThreshold) return <span className="no-value">&mdash;</span>;

  const first = breaches[0];
  if (first === undefined) return <span className="band-chip band-chip-ok">IN BAND</span>;

  return (
    <span className="band-chip band-chip-breached">
      {first.label}
      <small className="band-chip-limit">{first.limit}</small>
      {breaches.length > 1 && <small className="band-chip-limit">+{breaches.length - 1}</small>}
    </span>
  );
}

/**
 * The thresholds are the app's own, so every surface that judges a value prints them rather than
 * letting a red number imply the gateway said so.
 */
export function ThresholdStrip() {
  const { co2Ppm, pm25, humidityPercent } = ALERT_THRESHOLDS;
  return (
    <div className="threshold-strip">
      <span>ALERT THRESHOLDS &mdash; OURS, NOT THE GATEWAY&apos;S &mdash;</span>
      <span>
        CO2 <b className="threshold-value">&gt;{co2Ppm.max} PPM</b>
      </span>
      <span>
        PM2.5 <b className="threshold-value">&gt;{pm25.max} UG/M3</b>
      </span>
      <span>
        <span>RH </span>
        <b className="threshold-value">
          OUTSIDE {humidityPercent.min}-{humidityPercent.max}%
        </b>
      </span>
      <span className="threshold-note">ENERGY AND MOTION HAVE NO THRESHOLD</span>
    </div>
  );
}

/** Refetching never blanks a panel; only this strip moves. */
export function RefreshBar() {
  return (
    <output className="refresh-bar" aria-label="Refreshing">
      <i className="refresh-bar-fill" />
    </output>
  );
}
