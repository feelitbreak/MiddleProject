import type { AggregationInterval, ReadingMetric, SensorType } from '../graphql/generated/graphql';

export interface MetricOption {
  readonly value: ReadingMetric;
  readonly label: string;
  readonly unit: string;
}

export const METRICS: readonly MetricOption[] = [
  { value: 'CO2', label: 'CO2', unit: 'PPM' },
  { value: 'PM25', label: 'PM2.5', unit: 'UG/M3' },
  { value: 'HUMIDITY', label: 'HUMIDITY', unit: '%' },
  { value: 'MOTION_DETECTED', label: 'MOTION', unit: 'FRACTION' },
  { value: 'ENERGY_KWH', label: 'ENERGY', unit: 'KWH' },
];

export const INTERVALS: readonly AggregationInterval[] = ['HOUR', 'DAY', 'WEEK', 'MONTH'];

export const SENSOR_TYPES: readonly SensorType[] = ['AIR_QUALITY', 'MOTION', 'ENERGY'];

/**
 * Series colours read as location identity and never as state. Purple is absent on purpose: it is
 * reserved for what the viewer has filtered to.
 */
const SERIES_COLOURS = ['#7FDCAF', '#FF8FA9', '#2F9E6B', '#FFD24A', '#B3234C', '#0F7A52'] as const;

export function seriesColour(index: number): string {
  return SERIES_COLOURS[index % SERIES_COLOURS.length] ?? '#2A2140';
}

/**
 * The threshold the chart should draw for a metric, when one exists. Only the air-quality metrics
 * have one; a line across an energy chart would assert a limit nobody set.
 */
export function chartThreshold(metric: ReadingMetric): { value: number; label: string } | null {
  switch (metric) {
    case 'CO2':
      return { value: 1000, label: 'ALERT 1000 PPM' };
    case 'PM25':
      return { value: 15, label: 'ALERT 15 UG/M3' };
    case 'HUMIDITY':
      return { value: 60, label: 'ALERT 60%' };
    default:
      return null;
  }
}
