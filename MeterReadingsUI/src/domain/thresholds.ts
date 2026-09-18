import type { SensorType } from '../graphql/generated/graphql';

/**
 * Alert thresholds are ours, not the gateway's: the schema exposes raw numbers and no notion of a
 * healthy value. These are real-world reference points rather than values chosen to fire often
 * against WeakApp's uniformly random output, so CO2 in particular will rarely trip.
 */
export const ALERT_THRESHOLDS = {
  /** Common indoor ventilation indicator, not a safety limit. */
  co2Ppm: { max: 1000 },
  /** WHO 24-hour air quality guideline. */
  pm25: { max: 15 },
  /** Usual indoor comfort range. */
  humidityPercent: { min: 30, max: 60 },
} as const;

/** Energy and motion carry no threshold: there is no such thing as an unhealthy kWh. */
export const TYPES_WITHOUT_THRESHOLDS: readonly SensorType[] = ['ENERGY', 'MOTION'];

export interface Breach {
  readonly label: string;
  readonly limit: string;
}

export interface Measurable {
  readonly co2?: number | null;
  readonly pm25?: number | null;
  readonly humidity?: number | null;
}

export function breachesFor(reading: Measurable): Breach[] {
  const breaches: Breach[] = [];
  const { co2Ppm, pm25, humidityPercent } = ALERT_THRESHOLDS;

  if (reading.co2 != null && reading.co2 > co2Ppm.max) {
    breaches.push({ label: 'CO2 HIGH', limit: `>${co2Ppm.max}` });
  }
  if (reading.pm25 != null && reading.pm25 > pm25.max) {
    breaches.push({ label: 'PM2.5 HIGH', limit: `>${pm25.max}` });
  }
  if (reading.humidity != null) {
    if (reading.humidity < humidityPercent.min) {
      breaches.push({ label: 'RH LOW', limit: `<${humidityPercent.min}` });
    } else if (reading.humidity > humidityPercent.max) {
      breaches.push({ label: 'RH HIGH', limit: `>${humidityPercent.max}` });
    }
  }
  return breaches;
}

export function isBreached(metric: 'co2' | 'pm25' | 'humidity', value: number | null): boolean {
  if (value == null) return false;
  switch (metric) {
    case 'co2':
      return value > ALERT_THRESHOLDS.co2Ppm.max;
    case 'pm25':
      return value > ALERT_THRESHOLDS.pm25.max;
    case 'humidity':
      return (
        value < ALERT_THRESHOLDS.humidityPercent.min || value > ALERT_THRESHOLDS.humidityPercent.max
      );
  }
}
