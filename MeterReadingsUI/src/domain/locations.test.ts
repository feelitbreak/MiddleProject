import { groupByLocation, typesPresent } from './locations';
import {
  airQualityReading,
  at,
  energyReading,
  fullLocation,
  motionReading,
  NOW,
} from '../testing/fixtures';

describe('groupByLocation', () => {
  it('should collapse a location three sensor rows into one row with every column filled', () => {
    const rows = groupByLocation(fullLocation('Kitchen'), NOW);

    expect(rows).toHaveLength(1);
    const row = rows[0];
    expect(row?.location).toBe('Kitchen');
    expect(row?.airQuality?.co2).toBe(500);
    expect(row?.motion?.detected).toBe(true);
    expect(row?.energy?.kwh).toBe(420.5);
  });

  it('should sort locations by name so the table order does not follow the query order', () => {
    const readings = [
      ...fullLocation('Office'),
      ...fullLocation('Bedroom'),
      ...fullLocation('Garage'),
    ];

    expect(groupByLocation(readings, NOW).map((r) => r.location)).toEqual([
      'Bedroom',
      'Garage',
      'Office',
    ]);
  });

  it('should keep each sensor own age when they drift apart', () => {
    const rows = groupByLocation(
      [
        airQualityReading({ location: 'Kitchen', collectedAt: at(5) }),
        motionReading({ location: 'Kitchen', collectedAt: at(300) }),
        energyReading({ location: 'Kitchen', collectedAt: at(1000) }),
      ],
      NOW,
    );

    const row = rows[0];
    expect(row?.airQuality?.state).toBe('live');
    expect(row?.motion?.state).toBe('stale');
    expect(row?.energy?.state).toBe('lost');
  });

  it('should report the worst state so one dead feed cannot hide behind two live ones', () => {
    const rows = groupByLocation(
      [
        airQualityReading({ location: 'Kitchen', collectedAt: at(5) }),
        motionReading({ location: 'Kitchen', collectedAt: at(5) }),
        energyReading({ location: 'Kitchen', collectedAt: at(2000) }),
      ],
      NOW,
    );

    expect(rows[0]?.worst).toBe('lost');
  });

  it('should carry the oldest timestamp, not the newest', () => {
    const oldest = at(1000);
    const rows = groupByLocation(
      [
        airQualityReading({ location: 'Kitchen', collectedAt: at(5) }),
        energyReading({ location: 'Kitchen', collectedAt: oldest }),
      ],
      NOW,
    );

    expect(rows[0]?.oldestCollectedAt).toBe(oldest);
  });

  it('should leave absent sensor types null rather than inventing a slot', () => {
    const rows = groupByLocation([airQualityReading({ location: 'Kitchen' })], NOW);

    expect(rows[0]?.airQuality).not.toBeNull();
    expect(rows[0]?.motion).toBeNull();
    expect(rows[0]?.energy).toBeNull();
  });

  it('should return nothing for an empty catalogue', () => {
    expect(groupByLocation([], NOW)).toEqual([]);
  });
});

describe('typesPresent', () => {
  it('should list the types the location actually reports, in a fixed order', () => {
    const [row] = groupByLocation(fullLocation('Kitchen'), NOW);
    expect(row && typesPresent(row)).toBe('AIR_QUALITY + MOTION + ENERGY');
  });

  it('should list only what is there', () => {
    const [row] = groupByLocation([energyReading({ location: 'Kitchen' })], NOW);
    expect(row && typesPresent(row)).toBe('ENERGY');
  });
});
