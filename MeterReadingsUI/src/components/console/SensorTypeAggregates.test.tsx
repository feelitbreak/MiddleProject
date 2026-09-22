import { render, screen } from '@testing-library/react';
import SensorTypeAggregates from './SensorTypeAggregates';
import { groupByLocation } from '../../domain/locations';
import { airQualityReading, at, energyReading, motionReading, NOW } from '../../testing/fixtures';

function renderFor(readings: Parameters<typeof groupByLocation>[0]) {
  return render(<SensorTypeAggregates rows={groupByLocation(readings, NOW)} />);
}

describe('SensorTypeAggregates', () => {
  it('should average CO2 across the reporting air-quality sensors', () => {
    renderFor([
      airQualityReading({ location: 'Kitchen', co2: 400 }),
      airQualityReading({ location: 'Office', co2: 600 }),
    ]);

    expect(screen.getByText('500')).toBeInTheDocument();
    expect(screen.getAllByText(/2 SENSORS REPORTING/)[0]).toBeInTheDocument();
  });

  it('should report motion as the fraction of sensors currently detecting', () => {
    renderFor([
      motionReading({ location: 'Kitchen', motionDetected: true }),
      motionReading({ location: 'Office', motionDetected: false }),
      motionReading({ location: 'Garage', motionDetected: false }),
    ]);

    expect(screen.getByText('33%')).toBeInTheDocument();
  });

  /** A lost sensor has no current value, so averaging it in would invent one. */
  it('should exclude lost sensors from every average', () => {
    renderFor([
      airQualityReading({ location: 'Kitchen', co2: 400, collectedAt: at(5) }),
      airQualityReading({ location: 'Office', co2: 900, collectedAt: at(5000) }),
    ]);

    expect(screen.getByText('400')).toBeInTheDocument();
    expect(screen.getAllByText(/1 SENSORS REPORTING/)[0]).toBeInTheDocument();
  });

  it('should count how many locations have gone dark', () => {
    renderFor([energyReading({ location: 'Office', collectedAt: at(5000) })]);

    expect(screen.getByText(/1 LOCATION LOST/)).toBeInTheDocument();
  });

  it('should show a dash rather than NaN when nothing is reporting', () => {
    renderFor([]);

    expect(screen.getAllByText('--')).toHaveLength(3);
  });

  it('should keep a sensor type visible even when no sensor of that type exists', () => {
    renderFor([airQualityReading({ location: 'Kitchen' })]);

    expect(screen.getByText(/AIR_QUALITY/)).toBeInTheDocument();
    expect(screen.getByText(/MOTION/)).toBeInTheDocument();
    expect(screen.getByText(/ENERGY/)).toBeInTheDocument();
  });
});
