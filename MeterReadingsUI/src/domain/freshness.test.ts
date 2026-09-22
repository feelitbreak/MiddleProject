import { feedStateAt, formatAge, formatClock, formatDay, worseState } from './freshness';
import { at, NOW } from '../testing/fixtures';

describe('feedStateAt', () => {
  it('should call a reading live inside the first two minutes', () => {
    expect(feedStateAt(at(0), NOW)).toBe('live');
    expect(feedStateAt(at(119), NOW)).toBe('live');
  });

  it('should turn stale exactly on the two-minute boundary', () => {
    expect(feedStateAt(at(120), NOW)).toBe('stale');
  });

  it('should stay stale until the fifteen-minute boundary', () => {
    expect(feedStateAt(at(899), NOW)).toBe('stale');
  });

  it('should be lost from fifteen minutes onwards', () => {
    expect(feedStateAt(at(900), NOW)).toBe('lost');
    expect(feedStateAt(at(60 * 60 * 5), NOW)).toBe('lost');
  });

  it('should treat a clock skewed into the future as live rather than lost', () => {
    expect(feedStateAt(new Date(NOW + 30_000).toISOString(), NOW)).toBe('live');
  });
});

describe('worseState', () => {
  it('should pick the worse of the two in either order', () => {
    expect(worseState('live', 'stale')).toBe('stale');
    expect(worseState('stale', 'live')).toBe('stale');
    expect(worseState('stale', 'lost')).toBe('lost');
    expect(worseState('lost', 'live')).toBe('lost');
  });

  it('should return the state itself when both agree', () => {
    expect(worseState('live', 'live')).toBe('live');
    expect(worseState('lost', 'lost')).toBe('lost');
  });
});

describe('formatAge', () => {
  it('should use seconds below a minute', () => {
    expect(formatAge(at(0), NOW)).toBe('0s');
    expect(formatAge(at(59), NOW)).toBe('59s');
  });

  it('should use whole minutes below an hour', () => {
    expect(formatAge(at(60), NOW)).toBe('1m');
    expect(formatAge(at(59 * 60), NOW)).toBe('59m');
  });

  it('should pad the minutes when it reaches hours', () => {
    expect(formatAge(at(60 * 60), NOW)).toBe('1h00');
    expect(formatAge(at(60 * 60 + 29 * 60), NOW)).toBe('1h29');
  });

  it('should never render a negative age from a future timestamp', () => {
    expect(formatAge(new Date(NOW + 5000).toISOString(), NOW)).toBe('0s');
  });
});

describe('formatClock and formatDay', () => {
  // Asserted by shape rather than exact text: both delegate to the host locale.
  it('should render a 24-hour clock', () => {
    expect(formatClock('2026-09-17T14:22:35.000Z')).toMatch(/^\d{2}:\d{2}:\d{2}$/);
  });

  it('should render an uppercase day carrying the month and year', () => {
    const day = formatDay('2026-09-17T14:22:35.000Z');
    expect(day).toBe(day.toUpperCase());
    expect(day).toContain('2026');
    expect(day).toMatch(/SEP/);
  });
});
