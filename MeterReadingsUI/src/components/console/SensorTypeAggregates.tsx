import Window from '../shared/Window';
import type { LocationRow } from '../../domain/locations';

interface SensorTypeAggregatesProps {
  rows: readonly LocationRow[];
}

function mean(values: readonly number[]): number | null {
  if (values.length === 0) return null;
  return values.reduce((sum, v) => sum + v, 0) / values.length;
}

/**
 * Aggregation by sensor type, computed from the latest values already on screen rather than by
 * issuing three more aggregate queries: this panel answers "what is each type reading now", which
 * is exactly what `latestReadings` returned.
 */
export default function SensorTypeAggregates({ rows }: Readonly<SensorTypeAggregatesProps>) {
  const reporting = <T extends { state: string }>(slots: readonly (T | null)[]): T[] =>
    slots.filter((slot): slot is T => slot !== null && slot.state !== 'lost');

  const air = reporting(rows.map((r) => r.airQuality));
  const motion = reporting(rows.map((r) => r.motion));
  const energy = reporting(rows.map((r) => r.energy));

  const avgCo2 = mean(air.map((s) => s.co2).filter((v): v is number => v !== null));
  const motionFraction = mean(motion.map((s) => (s.detected === true ? 1 : 0)));
  const avgKwh = mean(energy.map((s) => s.kwh).filter((v): v is number => v !== null));

  const lostCount = rows.filter((r) => r.worst === 'lost').length;

  return (
    <Window title="AGGREGATE &mdash; BY SENSOR TYPE">
      <div className="type-card type-card-air">
        <span className="type-name">
          AIR_QUALITY <span className="type-unit">avg ppm</span>
        </span>
        <span className="type-value tabular">{avgCo2 === null ? '--' : Math.round(avgCo2)}</span>
        <span className="type-meta">{air.length} SENSORS REPORTING</span>
      </div>
      <div className="type-card type-card-motion">
        <span className="type-name">
          MOTION <span className="type-unit">fraction</span>
        </span>
        <span className="type-value tabular">
          {motionFraction === null ? '--' : `${Math.round(motionFraction * 100)}%`}
        </span>
        <span className="type-meta">{motion.length} SENSORS REPORTING</span>
      </div>
      <div className="type-card type-card-energy">
        <span className="type-name">
          ENERGY <span className="type-unit">avg kWh</span>
        </span>
        <span className="type-value tabular">{avgKwh === null ? '--' : Math.round(avgKwh)}</span>
        <span className="type-meta">
          {energy.length} SENSORS REPORTING{lostCount > 0 ? ` · ${lostCount} LOCATION LOST` : ''}
        </span>
      </div>
    </Window>
  );
}
