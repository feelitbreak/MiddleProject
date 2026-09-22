import { ALERT_THRESHOLDS, breachesFor, isBreached } from './thresholds';

describe('breachesFor', () => {
  it('should report nothing when every value sits inside its threshold', () => {
    expect(breachesFor({ co2: 500, pm25: 10, humidity: 45 })).toEqual([]);
  });

  it('should treat a value exactly on the limit as inside it', () => {
    const { co2Ppm, pm25, humidityPercent } = ALERT_THRESHOLDS;
    expect(breachesFor({ co2: co2Ppm.max, pm25: pm25.max, humidity: humidityPercent.max })).toEqual(
      [],
    );
    expect(breachesFor({ humidity: humidityPercent.min })).toEqual([]);
  });

  it('should report CO2 above the limit with the limit it used', () => {
    expect(breachesFor({ co2: ALERT_THRESHOLDS.co2Ppm.max + 1 })).toEqual([
      { label: 'CO2 HIGH', limit: '>1000' },
    ]);
  });

  it('should distinguish humidity below the range from above it', () => {
    expect(breachesFor({ humidity: 20 })).toEqual([{ label: 'RH LOW', limit: '<30' }]);
    expect(breachesFor({ humidity: 80 })).toEqual([{ label: 'RH HIGH', limit: '>60' }]);
  });

  it('should report every breach when a reading trips more than one', () => {
    const breaches = breachesFor({ co2: 1200, pm25: 40, humidity: 90 });
    expect(breaches.map((b) => b.label)).toEqual(['CO2 HIGH', 'PM2.5 HIGH', 'RH HIGH']);
  });

  it('should ignore values the sensor does not report', () => {
    expect(breachesFor({ co2: null, pm25: undefined, humidity: null })).toEqual([]);
    expect(breachesFor({})).toEqual([]);
  });
});

describe('isBreached', () => {
  it('should return false for a missing value rather than treating null as zero', () => {
    expect(isBreached('co2', null)).toBe(false);
    expect(isBreached('humidity', null)).toBe(false);
  });

  it('should agree with breachesFor on each metric', () => {
    expect(isBreached('co2', 1001)).toBe(true);
    expect(isBreached('co2', 1000)).toBe(false);
    expect(isBreached('pm25', 16)).toBe(true);
    expect(isBreached('pm25', 15)).toBe(false);
    expect(isBreached('humidity', 29)).toBe(true);
    expect(isBreached('humidity', 61)).toBe(true);
    expect(isBreached('humidity', 45)).toBe(false);
  });
});
