import { render, screen } from '@testing-library/react';
import Arrivals from './Arrivals';
import { groupByLocation } from '../../domain/locations';
import { airQualityReading, at, NOW } from '../../testing/fixtures';
import type { ReadingsChanged } from '../../hooks/useLiveReadings';

const EVENT: ReadingsChanged = {
  publishedAt: new Date(NOW - 14_000).toISOString(),
  readingCount: 18,
  newestCollectedAt: new Date(NOW - 16_000).toISOString(),
  sensors: [
    { location: 'Kitchen', sensorType: 'air_quality' },
    { location: 'Garage', sensorType: 'motion' },
  ],
};

function renderArrivals(event: ReadingsChanged | null, seen = 0, readings = []) {
  return render(
    <Arrivals
      lastEvent={event}
      eventsSeen={seen}
      rows={groupByLocation(readings, NOW)}
      now={NOW}
    />,
  );
}

describe('Arrivals', () => {
  it('should say it is waiting before the first event rather than looking broken', () => {
    renderArrivals(null);

    expect(screen.getByText('NO EVENTS YET')).toBeInTheDocument();
    expect(screen.getByText('HUB IDLE')).toBeInTheDocument();
  });

  it('should list the sensors the event named', () => {
    renderArrivals(EVENT);

    expect(screen.getByText('KITCHEN')).toBeInTheDocument();
    expect(screen.getByText('GARAGE')).toBeInTheDocument();
  });

  it('should translate the stored sensor-type spelling into a label', () => {
    renderArrivals(EVENT);

    expect(screen.getByText('AIR QUALITY')).toBeInTheDocument();
    expect(screen.queryByText('air_quality')).not.toBeInTheDocument();
  });

  it('should summarise the event in its query line', () => {
    renderArrivals(EVENT);

    expect(screen.getByText(/18 rows · 2 sensors · 14s ago/)).toBeInTheDocument();
  });

  it('should count the events seen this session', () => {
    renderArrivals(EVENT, 7);

    expect(screen.getByText('EVENTS THIS SESSION')).toBeInTheDocument();
    expect(screen.getByText('7')).toBeInTheDocument();
  });

  it('should raise a notice for every location that has gone dark', () => {
    render(
      <Arrivals
        lastEvent={EVENT}
        eventsSeen={1}
        rows={groupByLocation(
          [
            airQualityReading({ location: 'Office', collectedAt: at(5000) }),
            airQualityReading({ location: 'Bedroom', collectedAt: at(9000) }),
          ],
          NOW,
        )}
        now={NOW}
      />,
    );

    expect(screen.getByText(/SIGNAL LOST · OFFICE/)).toBeInTheDocument();
    expect(screen.getByText(/SIGNAL LOST · BEDROOM/)).toBeInTheDocument();
  });

  it('should say it is showing no value rather than a stale one', () => {
    render(
      <Arrivals
        lastEvent={EVENT}
        eventsSeen={1}
        rows={groupByLocation(
          [airQualityReading({ location: 'Office', collectedAt: at(5000) })],
          NOW,
        )}
        now={NOW}
      />,
    );

    expect(screen.getByText(/no value rather than a stale one/i)).toBeInTheDocument();
  });

  it('should raise no notice while every location is reporting', () => {
    render(
      <Arrivals
        lastEvent={EVENT}
        eventsSeen={1}
        rows={groupByLocation([airQualityReading({ location: 'Office', collectedAt: at(5) })], NOW)}
        now={NOW}
      />,
    );

    expect(screen.queryByText(/SIGNAL LOST/)).not.toBeInTheDocument();
  });
});
