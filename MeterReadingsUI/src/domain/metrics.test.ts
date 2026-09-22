import { chartThreshold, INTERVALS, METRICS, SENSOR_TYPES, seriesColour } from './metrics';

describe('METRICS', () => {
  it('should cover every metric the schema exposes', () => {
    expect(METRICS.map((m) => m.value)).toEqual([
      'CO2',
      'PM25',
      'HUMIDITY',
      'MOTION_DETECTED',
      'ENERGY_KWH',
    ]);
  });

  it('should give every metric a label and a unit', () => {
    for (const metric of METRICS) {
      expect(metric.label).not.toHaveLength(0);
      expect(metric.unit).not.toHaveLength(0);
    }
  });
});

describe('INTERVALS and SENSOR_TYPES', () => {
  it('should cover every interval the schema accepts', () => {
    expect(INTERVALS).toEqual(['HOUR', 'DAY', 'WEEK', 'MONTH']);
  });

  it('should cover every sensor type', () => {
    expect(SENSOR_TYPES).toEqual(['AIR_QUALITY', 'MOTION', 'ENERGY']);
  });
});

describe('seriesColour', () => {
  it('should give consecutive series different colours', () => {
    expect(seriesColour(0)).not.toBe(seriesColour(1));
  });

  it('should wrap rather than run out when there are more series than colours', () => {
    expect(seriesColour(6)).toBe(seriesColour(0));
    expect(seriesColour(13)).toBe(seriesColour(1));
  });

  /** Purple means "what you filtered to"; a series wearing it would claim a selection. */
  it('should never use the reserved filter colour', () => {
    for (let i = 0; i < 12; i++) {
      expect(seriesColour(i).toLowerCase()).not.toBe('#b9a9f0');
    }
  });
});

describe('chartThreshold', () => {
  it('should draw a line for the metrics that have one', () => {
    expect(chartThreshold('CO2')).toEqual({ value: 1000, label: 'ALERT 1000 PPM' });
    expect(chartThreshold('PM25')?.value).toBe(15);
    expect(chartThreshold('HUMIDITY')?.value).toBe(60);
  });

  /** A line across an energy chart would assert a limit nobody set. */
  it('should draw nothing for metrics with no threshold', () => {
    expect(chartThreshold('ENERGY_KWH')).toBeNull();
    expect(chartThreshold('MOTION_DETECTED')).toBeNull();
  });
});
