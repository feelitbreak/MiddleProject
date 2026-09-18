import { feedStateAt, worseState, type FeedState } from './freshness';
import type { LatestReadingsQuery } from '../graphql/generated/graphql';

type Reading = LatestReadingsQuery['latestReadings'][number];

interface Slot {
  readonly collectedAt: string;
  readonly state: FeedState;
}

export interface AirQualitySlot extends Slot {
  readonly co2: number | null;
  readonly pm25: number | null;
  readonly humidity: number | null;
}

export interface MotionSlot extends Slot {
  readonly detected: boolean | null;
}

export interface EnergySlot extends Slot {
  readonly kwh: number | null;
}

export interface LocationRow {
  readonly location: string;
  readonly airQuality: AirQualitySlot | null;
  readonly motion: MotionSlot | null;
  readonly energy: EnergySlot | null;
  /** Worst state across the location's sensors, so one dead feed cannot hide behind two live ones. */
  readonly worst: FeedState;
  readonly oldestCollectedAt: string | null;
}

/**
 * Every location carries all three sensor types, so `latestReadings` returns three rows per
 * location. Pivoting them into one row per location fills every column instead of leaving two
 * thirds of each row empty. Each sensor keeps its own age, because they can drift apart.
 */
export function groupByLocation(readings: readonly Reading[], now: number): LocationRow[] {
  const byLocation = new Map<string, Reading[]>();
  for (const reading of readings) {
    const group = byLocation.get(reading.sensor.name);
    if (group) group.push(reading);
    else byLocation.set(reading.sensor.name, [reading]);
  }

  const rows: LocationRow[] = [];
  for (const [location, group] of byLocation) {
    const air = group.find((r) => r.sensor.type === 'AIR_QUALITY');
    const motion = group.find((r) => r.sensor.type === 'MOTION');
    const energy = group.find((r) => r.sensor.type === 'ENERGY');

    let worst: FeedState = 'live';
    let oldest: string | null = null;
    for (const reading of group) {
      worst = worseState(worst, feedStateAt(reading.collectedAt, now));
      if (oldest === null || Date.parse(reading.collectedAt) < Date.parse(oldest)) {
        oldest = reading.collectedAt;
      }
    }

    rows.push({
      location,
      airQuality: air
        ? {
            co2: air.co2,
            pm25: air.pm25,
            humidity: air.humidity,
            collectedAt: air.collectedAt,
            state: feedStateAt(air.collectedAt, now),
          }
        : null,
      motion: motion
        ? {
            detected: motion.motionDetected,
            collectedAt: motion.collectedAt,
            state: feedStateAt(motion.collectedAt, now),
          }
        : null,
      energy: energy
        ? {
            kwh: energy.energyKwh,
            collectedAt: energy.collectedAt,
            state: feedStateAt(energy.collectedAt, now),
          }
        : null,
      worst,
      oldestCollectedAt: oldest,
    });
  }

  return rows.sort((a, b) => a.location.localeCompare(b.location));
}

/** Sensor types present at a location, for the row's subtitle. */
export function typesPresent(row: LocationRow): string {
  const present: string[] = [];
  if (row.airQuality) present.push('AIR_QUALITY');
  if (row.motion) present.push('MOTION');
  if (row.energy) present.push('ENERGY');
  return present.join(' + ');
}
