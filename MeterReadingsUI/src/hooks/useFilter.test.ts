import { act, renderHook } from '@testing-library/react';
import { useFilter } from './useFilter';

describe('useFilter', () => {
  it('should default to a 24-hour window with no other filter applied', () => {
    const { result } = renderHook(() => useFilter());

    expect(result.current.filter.location).toBeNull();
    expect(result.current.filter.sensorType).toBeNull();
    expect(result.current.filter.from).not.toBeNull();
    expect(result.current.filter.to).toBeNull();
    expect(result.current.active).toEqual([]);
  });

  it('should update one field without disturbing the others', () => {
    const { result } = renderHook(() => useFilter());
    const originalFrom = result.current.filter.from;

    act(() => result.current.set('location', 'Kitchen'));

    expect(result.current.filter.location).toBe('Kitchen');
    expect(result.current.filter.from).toBe(originalFrom);
    expect(result.current.filter.metric).toBe('CO2');
  });

  it('should project the same state into both input shapes', () => {
    const { result } = renderHook(() => useFilter());

    act(() => {
      result.current.set('location', 'Garage');
      result.current.set('sensorType', 'AIR_QUALITY');
    });

    expect(result.current.readingWhere.location).toBe('Garage');
    expect(result.current.aggregateWhere.location).toBe('Garage');
  });

  /** The metric already fixes the sensor type, so one in the aggregate input could only disagree. */
  it('should keep sensorType out of the aggregate input', () => {
    const { result } = renderHook(() => useFilter());

    act(() => result.current.set('sensorType', 'MOTION'));

    expect(result.current.readingWhere.sensorType).toBe('MOTION');
    expect(result.current.aggregateWhere).not.toHaveProperty('sensorType');
  });

  it('should raise a chip for each narrowing filter, but not for the defaults', () => {
    const { result } = renderHook(() => useFilter());

    act(() => {
      result.current.set('location', 'Office');
      result.current.set('search', 'off');
    });

    expect(result.current.active.map((chip) => chip.key)).toEqual(['location', 'search']);
    expect(result.current.active[0]?.label).toBe('LOCATION: Office');
  });

  it('should reset every field and restore the default window when cleared', () => {
    const { result } = renderHook(() => useFilter());

    act(() => {
      result.current.set('location', 'Office');
      result.current.set('sensorType', 'ENERGY');
      result.current.set('search', 'off');
      result.current.set('metric', 'PM25');
    });
    act(() => result.current.clear());

    expect(result.current.filter.location).toBeNull();
    expect(result.current.filter.sensorType).toBeNull();
    expect(result.current.filter.search).toBe('');
    expect(result.current.filter.metric).toBe('CO2');
    expect(result.current.filter.from).not.toBeNull();
    expect(result.current.active).toEqual([]);
  });

  it('should keep the where objects referentially stable when an unrelated field changes', () => {
    const { result } = renderHook(() => useFilter());
    const before = result.current.readingWhere;

    act(() => result.current.set('metric', 'HUMIDITY'));

    expect(result.current.readingWhere).toBe(before);
  });
});
