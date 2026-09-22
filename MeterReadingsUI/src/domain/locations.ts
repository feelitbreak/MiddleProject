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
function bySensorName(readings: readonly Reading[]): Map<string, Reading[]> {
  const byLocation = new Map<string, Reading[]>();
  for (const reading of readings) {
    const group = byLocation.get(reading.sensor.name);
    if (group) group.push(reading);
    else byLocation.set(reading.sensor.name, [reading]);
  }
  return byLocation;
}

function worstOf(group: readonly Reading[], now: number): FeedState {
  return group.reduce<FeedState>(
    (worst, reading) => worseState(worst, feedStateAt(reading.collectedAt, now)),
    'live',
  );
}

function oldestOf(group: readonly Reading[]): string | null {
  return group.reduce<string | null>(
    (oldest, reading) =>
      oldest === null || Date.parse(reading.collectedAt) < Date.parse(oldest)
        ? reading.collectedAt
        : oldest,
    null,
  );
}

function toRow(location: string, group: readonly Reading[], now: number): LocationRow {
  const find = (type: Reading['sensor']['type']) => group.find((r) => r.sensor.type === type);
  const air = find('AIR_QUALITY');
  const motion = find('MOTION');
  const energy = find('ENERGY');

  return {
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
    worst: worstOf(group, now),
    oldestCollectedAt: oldestOf(group),
  };
}

export function groupByLocation(readings: readonly Reading[], now: number): LocationRow[] {
  return [...bySensorName(readings)]
    .map(([location, group]) => toRow(location, group, now))
    .sort((a, b) => a.location.localeCompare(b.location));
}

/** Sensor types present at a location, for the row's subtitle. */
export function typesPresent(row: LocationRow): string {
  const present: string[] = [];
  if (row.airQuality) present.push('AIR_QUALITY');
  if (row.motion) present.push('MOTION');
  if (row.energy) present.push('ENERGY');
  return present.join(' + ');
}
