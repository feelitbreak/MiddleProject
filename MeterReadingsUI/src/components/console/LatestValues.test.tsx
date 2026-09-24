import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import LatestValues from './LatestValues';
import { groupByLocation } from '../../domain/locations';
import {
  airQualityReading,
  at,
  energyReading,
  fullLocation,
  motionReading,
  NOW,
} from '../../testing/fixtures';
import type { LocationRow } from '../../domain/locations';

function renderPanel(rows: readonly LocationRow[]) {
  return render(
    <MemoryRouter>
      <LatestValues
        rows={rows}
        sensorCount={rows.length * 3}
        loading={false}
        refreshing={false}
        now={NOW}
        query="q"
      />
    </MemoryRouter>,
  );
}

/** The desktop table and the mobile cards both render; assert against the table. */
function tableRow(location: string) {
  const cell = screen.getAllByText(location)[0];
  const row = cell?.closest('tr');
  if (!row) throw new Error(`No table row for ${location}`);
  return within(row);
}

describe('LatestValues', () => {
  it('should render one row per location with every column filled', () => {
    renderPanel(groupByLocation(fullLocation('Kitchen'), NOW));

    const row = tableRow('KITCHEN');
    expect(row.getByText('500')).toBeInTheDocument();
    expect(row.getByText('10')).toBeInTheDocument();
    expect(row.getByText('45')).toBeInTheDocument();
    expect(row.getByText('YES')).toBeInTheDocument();
    expect(row.getByText('420.5')).toBeInTheDocument();
  });

  it('should tint an out-of-band value without filling the cell', () => {
    renderPanel(groupByLocation([airQualityReading({ location: 'Office', co2: 1200 })], NOW));

    const value = screen.getAllByText('1200')[0];
    expect(value).toHaveClass('out-of-band');
  });

  it('should leave an in-band value untinted', () => {
    renderPanel(groupByLocation([airQualityReading({ location: 'Office', co2: 500 })], NOW));
    expect(screen.getAllByText('500')[0]).not.toHaveClass('out-of-band');
  });

  /** A value that stopped being true is worse than no value at all. */
  it('should hide the readings of a lost sensor rather than showing a stale number', () => {
    const rows = groupByLocation(
      [airQualityReading({ location: 'Office', co2: 742, collectedAt: at(3000) })],
      NOW,
    );

    renderPanel(rows);

    expect(screen.queryByText('742')).not.toBeInTheDocument();
    expect(screen.getAllByText('LOST')[0]).toBeInTheDocument();
  });

  it('should keep showing a stale reading, because it was true when it was taken', () => {
    const rows = groupByLocation(
      [airQualityReading({ location: 'Office', co2: 742, collectedAt: at(300) })],
      NOW,
    );

    renderPanel(rows);

    expect(screen.getAllByText('742')[0]).toBeInTheDocument();
    expect(screen.getAllByText('STALE')[0]).toBeInTheDocument();
  });

  it('should report feed age and value band independently', () => {
    const rows = groupByLocation(
      [airQualityReading({ location: 'Office', co2: 1200, collectedAt: at(300) })],
      NOW,
    );

    renderPanel(rows);

    expect(screen.getAllByText('STALE')[0]).toBeInTheDocument();
    expect(screen.getAllByText('CO2 HIGH')[0]).toBeInTheDocument();
  });

  it('should show no band verdict for a location that only reports energy', () => {
    renderPanel(groupByLocation([energyReading({ location: 'Garage' })], NOW));

    expect(screen.queryByText('IN BAND')).not.toBeInTheDocument();
    expect(screen.getAllByText('LIVE')[0]).toBeInTheDocument();
  });

  it('should show the worst feed state for the location', () => {
    const rows = groupByLocation(
      [
        airQualityReading({ location: 'Garage', collectedAt: at(5) }),
        motionReading({ location: 'Garage', collectedAt: at(5000) }),
      ],
      NOW,
    );

    renderPanel(rows);

    expect(screen.getAllByText('LOST')[0]).toBeInTheDocument();
  });

  it('should explain an empty catalogue instead of rendering a bare table', () => {
    renderPanel([]);

    expect(screen.getByText('NO SENSORS MATCH')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('should show the refresh strip only while refetching', () => {
    const rows = groupByLocation(fullLocation('Kitchen'), NOW);
    const { rerender } = renderPanel(rows);

    expect(screen.queryByRole('status')).not.toBeInTheDocument();

    rerender(
      <MemoryRouter>
        <LatestValues rows={rows} sensorCount={3} loading={false} refreshing now={NOW} query="q" />
      </MemoryRouter>,
    );

    expect(screen.getByRole('status')).toBeInTheDocument();
    // The values stay put: a refetch never blanks the panel.
    expect(screen.getAllByText('500')[0]).toBeInTheDocument();
  });
});
