import { useCallback, useMemo, useState } from 'react';
import type {
  AggregateFilterInput,
  AggregationInterval,
  ReadingFilterInput,
  ReadingMetric,
  SensorType,
} from '../graphql/generated/graphql';

export interface Filter {
  readonly location: string | null;
  readonly sensorType: SensorType | null;
  readonly from: string | null;
  readonly to: string | null;
  readonly search: string;
  readonly metric: ReadingMetric;
  readonly interval: AggregationInterval;
}

export interface ActiveFilter {
  readonly key: keyof Filter;
  readonly label: string;
}

const DEFAULT_RANGE_HOURS = 24;

function defaultFrom(): string {
  return new Date(Date.now() - DEFAULT_RANGE_HOURS * 60 * 60 * 1000).toISOString();
}

export const EMPTY_FILTER: Filter = {
  location: null,
  sensorType: null,
  from: null,
  to: null,
  search: '',
  metric: 'CO2',
  interval: 'HOUR',
};

export interface UseFilter {
  readonly filter: Filter;
  readonly set: <K extends keyof Filter>(key: K, value: Filter[K]) => void;
  readonly clear: () => void;
  readonly readingWhere: ReadingFilterInput;
  readonly aggregateWhere: AggregateFilterInput;
  readonly active: readonly ActiveFilter[];
}

/**
 * One filter object feeds every panel. The schema is shaped for exactly this: `readings` and
 * `readingAggregates` both take a `where`, so the two inputs below are projections of one state
 * rather than separate pieces of it.
 */
export function useFilter(initial: Filter = EMPTY_FILTER): UseFilter {
  const [filter, setFilter] = useState<Filter>({ ...initial, from: initial.from ?? defaultFrom() });

  const set = useCallback(<K extends keyof Filter>(key: K, value: Filter[K]) => {
    setFilter((current) => ({ ...current, [key]: value }));
  }, []);

  const clear = useCallback(() => setFilter({ ...EMPTY_FILTER, from: defaultFrom() }), []);

  const readingWhere = useMemo<ReadingFilterInput>(
    () => ({
      location: filter.location,
      sensorType: filter.sensorType,
      from: filter.from,
      to: filter.to,
    }),
    [filter.location, filter.sensorType, filter.from, filter.to],
  );

  // The aggregate input has no sensorType: the metric already fixes it, so one here could only disagree.
  const aggregateWhere = useMemo<AggregateFilterInput>(
    () => ({ location: filter.location, from: filter.from, to: filter.to }),
    [filter.location, filter.from, filter.to],
  );

  const active = useMemo<ActiveFilter[]>(() => {
    const chips: ActiveFilter[] = [];
    if (filter.location) chips.push({ key: 'location', label: `LOCATION: ${filter.location}` });
    if (filter.sensorType) chips.push({ key: 'sensorType', label: `TYPE: ${filter.sensorType}` });
    if (filter.to) chips.push({ key: 'to', label: 'TO: SET' });
    if (filter.search) chips.push({ key: 'search', label: `SEARCH: ${filter.search}` });
    return chips;
  }, [filter.location, filter.sensorType, filter.to, filter.search]);

  return { filter, set, clear, readingWhere, aggregateWhere, active };
}
