import { render, screen } from '@testing-library/react';
import { BandChip, FeedChip, ThresholdStrip } from './Indicators';

describe('FeedChip', () => {
  it.each([
    ['live', 'LIVE'],
    ['stale', 'STALE'],
    ['lost', 'LOST'],
  ] as const)('should name the %s state in words as well as colour', (state, word) => {
    render(<FeedChip state={state} age="4m" />);

    expect(screen.getByText(word)).toBeInTheDocument();
    expect(screen.getByText('4m')).toBeInTheDocument();
  });

  it('should carry the state in a class so the fill escalates with it', () => {
    const { container } = render(<FeedChip state="lost" age="1h29" />);
    expect(container.querySelector('.feed-chip-lost')).toBeInTheDocument();
  });
});

describe('BandChip', () => {
  /** Energy and motion have no threshold, so a band verdict would be an invention. */
  it('should show nothing but a dash when the reading has no threshold', () => {
    render(<BandChip breaches={[]} hasThreshold={false} />);

    expect(screen.queryByText('IN BAND')).not.toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument();
  });

  it('should say IN BAND when a thresholded reading trips nothing', () => {
    render(<BandChip breaches={[]} hasThreshold />);
    expect(screen.getByText('IN BAND')).toBeInTheDocument();
  });

  it('should print the limit alongside the breach so the number is never implied', () => {
    render(<BandChip breaches={[{ label: 'CO2 HIGH', limit: '>1000' }]} hasThreshold />);

    expect(screen.getByText('CO2 HIGH')).toBeInTheDocument();
    expect(screen.getByText('>1000')).toBeInTheDocument();
  });

  it('should count the remaining breaches when a reading trips more than one', () => {
    render(
      <BandChip
        breaches={[
          { label: 'CO2 HIGH', limit: '>1000' },
          { label: 'RH HIGH', limit: '>60' },
          { label: 'PM2.5 HIGH', limit: '>15' },
        ]}
        hasThreshold
      />,
    );

    expect(screen.getByText('CO2 HIGH')).toBeInTheDocument();
    expect(screen.getByText('+2')).toBeInTheDocument();
  });
});

describe('ThresholdStrip', () => {
  it('should state that the thresholds are the app own, not the gateway', () => {
    render(<ThresholdStrip />);
    expect(screen.getByText(/OURS, NOT THE GATEWAY/i)).toBeInTheDocument();
  });

  it('should print every threshold it judges against', () => {
    render(<ThresholdStrip />);

    expect(screen.getByText('>1000 PPM')).toBeInTheDocument();
    expect(screen.getByText('>15 UG/M3')).toBeInTheDocument();
    expect(screen.getByText('OUTSIDE 30-60%')).toBeInTheDocument();
  });

  it('should say which sensor types are not judged at all', () => {
    render(<ThresholdStrip />);
    expect(screen.getByText(/ENERGY AND MOTION HAVE NO THRESHOLD/)).toBeInTheDocument();
  });
});
