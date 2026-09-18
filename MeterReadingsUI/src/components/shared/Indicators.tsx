import type { FeedState } from '../../domain/freshness';
import type { Breach } from '../../domain/thresholds';
import { ALERT_THRESHOLDS } from '../../domain/thresholds';

const FEED_WORD: Record<FeedState, string> = {
  live: 'LIVE',
  stale: 'STALE',
  lost: 'LOST',
};

/** Feed age. Carries its word as well as its fill, so colour is never the only signal. */
export function FeedChip({ state, age }: { state: FeedState; age: string }) {
  return (
    <span className={`feed ${state}`}>
      <b>{FEED_WORD[state]}</b>
      <span className="n num">{age}</span>
    </span>
  );
}

/** Value band. Separate from feed age: a sensor can be stale and out of band at once. */
export function BandChip({
  breaches,
  hasThreshold,
}: {
  breaches: Breach[];
  hasThreshold: boolean;
}) {
  if (!hasThreshold) return <span className="na">&mdash;</span>;
  if (breaches.length === 0) return <span className="tag o">IN BAND</span>;
  const first = breaches[0];
  if (!first) return <span className="tag o">IN BAND</span>;
  return (
    <span className="tag h">
      {first.label}
      <small>{first.limit}</small>
      {breaches.length > 1 && <small>+{breaches.length - 1}</small>}
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
    <div className="bands">
      ALERT THRESHOLDS &mdash; OURS, NOT THE GATEWAY&apos;S &mdash;
      <span>
        CO2 <b>&gt;{co2Ppm.max} PPM</b>
      </span>
      <span>
        PM2.5 <b>&gt;{pm25.max} UG/M3</b>
      </span>
      <span>
        RH{' '}
        <b>
          OUTSIDE {humidityPercent.min}-{humidityPercent.max}%
        </b>
      </span>
      <span style={{ marginLeft: 'auto' }}>ENERGY AND MOTION HAVE NO THRESHOLD</span>
    </div>
  );
}

/** Refetching never blanks a panel; only this strip moves. */
export function LoadBar() {
  return (
    <div className="loadbar" role="status" aria-label="Refreshing">
      <i />
    </div>
  );
}
