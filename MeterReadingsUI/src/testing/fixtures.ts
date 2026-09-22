import type { LatestReadingsQuery, SensorType } from '../graphql/generated/graphql';

type Reading = LatestReadingsQuery['latestReadings'][number];

/** A fixed instant, so every age assertion is arithmetic rather than a race with the clock. */
export const NOW = Date.parse('2026-09-17T12:00:00.000Z');

export function at(secondsAgo: number): string {
  return new Date(NOW - secondsAgo * 1000).toISOString();
}

let nextId = 1;

export function airQualityReading(overrides: Partial<Reading> & { location: string }): Reading {
  const { location, ...rest } = overrides;
  return {
    __typename: 'Reading',
    id: nextId++,
    collectedAt: at(10),
    co2: 500,
    pm25: 10,
    humidity: 45,
    motionDetected: null,
    energyKwh: null,
    sensor: { __typename: 'Sensor', id: nextId, name: location, type: 'AIR_QUALITY' },
    ...rest,
  } as Reading;
}

export function motionReading(overrides: Partial<Reading> & { location: string }): Reading {
  const { location, ...rest } = overrides;
  return {
    __typename: 'Reading',
    id: nextId++,
    collectedAt: at(10),
    co2: null,
    pm25: null,
    humidity: null,
    motionDetected: true,
    energyKwh: null,
    sensor: { __typename: 'Sensor', id: nextId, name: location, type: 'MOTION' },
    ...rest,
  } as Reading;
}

export function energyReading(overrides: Partial<Reading> & { location: string }): Reading {
  const { location, ...rest } = overrides;
  return {
    __typename: 'Reading',
    id: nextId++,
    collectedAt: at(10),
    co2: null,
    pm25: null,
    humidity: null,
    motionDetected: null,
    energyKwh: 420.5,
    sensor: { __typename: 'Sensor', id: nextId, name: location, type: 'ENERGY' },
    ...rest,
  } as Reading;
}

/** The shape the real catalogue has: every location reports all three sensor types. */
export function fullLocation(location: string, collectedAt = at(10)): Reading[] {
  return [
    airQualityReading({ location, collectedAt }),
    motionReading({ location, collectedAt }),
    energyReading({ location, collectedAt }),
  ];
}

export const SENSOR_TYPES_IN_ORDER: readonly SensorType[] = ['AIR_QUALITY', 'MOTION', 'ENERGY'];
