import { render, screen } from '@testing-library/react';
import SteppedChart, { ChartLegend, type ChartSeries } from './SteppedChart';

const TICKS = ['10:00', 'NOW'];

function series(values: (number | null)[], overrides: Partial<ChartSeries> = {}): ChartSeries {
  return { name: 'KITCHEN', colour: '#7FDCAF', values, ...overrides };
}

function renderChart(list: ChartSeries[], threshold?: { value: number; label: string } | null) {
  return render(<SteppedChart series={list} ticks={TICKS} unit="ppm" threshold={threshold} />);
}

describe('SteppedChart', () => {
  it('should refuse to draw a line through fewer than two periods', () => {
    renderChart([series([500])]);

    expect(screen.getByText('NOT ENOUGH PERIODS')).toBeInTheDocument();
  });

  it('should draw an outline and a coloured line for each series', () => {
    const { container } = renderChart([
      series([400, 500, 600]),
      series([700, 800, 900], { name: 'OFFICE', colour: '#FF8FA9' }),
    ]);

    // Two polylines per series: the ink outline underneath and the colour on top.
    expect(container.querySelectorAll('polyline')).toHaveLength(4);
    expect(container.querySelector('polyline[stroke="#7FDCAF"]')).toBeInTheDocument();
    expect(container.querySelector('polyline[stroke="#FF8FA9"]')).toBeInTheDocument();
  });

  it('should mark every period with a square rather than a round point', () => {
    const { container } = renderChart([series([400, 500, 600])]);

    const markers = container.querySelectorAll('rect[stroke="#2A2140"]');
    expect(markers).toHaveLength(3);
    expect(container.querySelector('circle')).not.toBeInTheDocument();
  });

  /** A gap is drawn, never interpolated: a missing period is not a value. */
  it('should hatch the region where a series stops reporting', () => {
    const { container } = renderChart([series([400, 500, null, null], { lostAt: '08:12' })]);

    expect(container.querySelector('rect[fill="url(#lostHatch)"]')).toBeInTheDocument();
  });

  it('should label the gap with the series and the time it went quiet', () => {
    renderChart([series([400, 500, null, null], { lostAt: '08:12' })]);

    expect(screen.getByText('KITCHEN - NO DATA SINCE 08:12')).toBeInTheDocument();
  });

  it('should hatch without a label when the time is unknown', () => {
    const { container } = renderChart([series([400, 500, null, null])]);

    expect(container.querySelector('rect[fill="url(#lostHatch)"]')).toBeInTheDocument();
    expect(screen.queryByText(/NO DATA SINCE/)).not.toBeInTheDocument();
  });

  it('should not hatch anything when every series reports throughout', () => {
    const { container } = renderChart([series([400, 500, 600])]);

    expect(container.querySelector('rect[fill="url(#lostHatch)"]')).not.toBeInTheDocument();
  });

  it('should draw the threshold it judges against, labelled', () => {
    renderChart([series([400, 500, 600])], { value: 550, label: 'ALERT 550 PPM' });

    expect(screen.getByText('ALERT 550 PPM')).toBeInTheDocument();
  });

  /** A line pinned to the frame edge would read as data rather than a limit. */
  it('should omit a threshold that falls outside the plotted range', () => {
    renderChart([series([400, 500, 600])], { value: 5000, label: 'ALERT 5000 PPM' });

    expect(screen.queryByText('ALERT 5000 PPM')).not.toBeInTheDocument();
  });

  it('should omit the threshold line entirely for a metric that has none', () => {
    // Scoped by the dash, so the hatch pattern's own lines in <defs> are not mistaken for it.
    const withThreshold = renderChart([series([400, 500, 600])], {
      value: 550,
      label: 'ALERT 550 PPM',
    });
    expect(
      withThreshold.container.querySelector('line[stroke-dasharray="8 5"]'),
    ).toBeInTheDocument();
    withThreshold.unmount();

    const { container } = renderChart([series([400, 500, 600])], null);
    expect(container.querySelector('line[stroke-dasharray="8 5"]')).not.toBeInTheDocument();
  });

  it('should describe itself for a screen reader', () => {
    renderChart([series([400, 500, 600])]);

    expect(
      screen.getByRole('img', { name: /one series per location, in ppm/i }),
    ).toBeInTheDocument();
  });
});

describe('ChartLegend', () => {
  it('should name every series with its own colour', () => {
    const { container } = render(
      <ChartLegend
        series={[series([1, 2]), series([3, 4], { name: 'Office', colour: '#FF8FA9' })]}
      />,
    );

    expect(screen.getByText('KITCHEN')).toBeInTheDocument();
    expect(screen.getByText('OFFICE')).toBeInTheDocument();
    expect(container.querySelectorAll('.chart-legend-swatch')).toHaveLength(2);
  });

  it('should show the note when one is given, and nothing when not', () => {
    const { rerender } = render(<ChartLegend series={[series([1, 2])]} note="SERVER-SIDE CAPS" />);
    expect(screen.getByText('SERVER-SIDE CAPS')).toBeInTheDocument();

    rerender(<ChartLegend series={[series([1, 2])]} />);
    expect(screen.queryByText('SERVER-SIDE CAPS')).not.toBeInTheDocument();
  });
});
