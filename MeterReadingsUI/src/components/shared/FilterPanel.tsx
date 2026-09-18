import Window from './Window';
import { INTERVALS, METRICS, SENSOR_TYPES } from '../../domain/metrics';
import type { UseFilter } from '../../hooks/useFilter';
import type { ReadingMetric, SensorType } from '../../graphql/generated/graphql';

interface FilterPanelProps {
  controls: UseFilter;
  locations: readonly string[];
  /** The explorer pages; the console does not. */
  showPageSize?: boolean;
  pageSize?: number;
  onPageSize?: (size: number) => void;
}

const PAGE_SIZES = [25, 50, 100];

/** Datetime-local wants `YYYY-MM-DDTHH:mm`; the gateway wants ISO with an offset. */
function toLocalInput(iso: string | null): string {
  if (iso === null) return '';
  const date = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function inputClass(isActive: boolean): string {
  return isActive ? 'filter-input filter-input-active' : 'filter-input';
}

export default function FilterPanel({
  controls,
  locations,
  showPageSize = false,
  pageSize = 25,
  onPageSize,
}: FilterPanelProps) {
  const { filter, set, clear, active } = controls;

  return (
    <Window title="FILTER" tone="filter">
      <div className="filter-grid">
        <label className="filter-row">
          <span className="filter-label">LOCATION</span>
          <span className={inputClass(filter.location !== null)}>
            <select
              value={filter.location ?? ''}
              onChange={(e) => set('location', e.target.value === '' ? null : e.target.value)}
            >
              <option value="">ALL</option>
              {locations.map((location) => (
                <option key={location} value={location}>
                  {location}
                </option>
              ))}
            </select>
          </span>
        </label>

        <label className="filter-row">
          <span className="filter-label">TYPE</span>
          <span className={inputClass(filter.sensorType !== null)}>
            <select
              value={filter.sensorType ?? ''}
              onChange={(e) =>
                set('sensorType', e.target.value === '' ? null : (e.target.value as SensorType))
              }
            >
              <option value="">ALL</option>
              {SENSOR_TYPES.map((type) => (
                <option key={type} value={type}>
                  {type}
                </option>
              ))}
            </select>
          </span>
        </label>

        <label className="filter-row">
          <span className="filter-label">FROM</span>
          <span className="filter-input">
            <input
              type="datetime-local"
              value={toLocalInput(filter.from)}
              onChange={(e) =>
                set('from', e.target.value === '' ? null : new Date(e.target.value).toISOString())
              }
            />
          </span>
        </label>

        <label className="filter-row">
          <span className="filter-label">TO</span>
          <span className={inputClass(filter.to !== null)}>
            <input
              type="datetime-local"
              value={toLocalInput(filter.to)}
              onChange={(e) =>
                set('to', e.target.value === '' ? null : new Date(e.target.value).toISOString())
              }
            />
          </span>
        </label>

        <label className="filter-row">
          <span className="filter-label">SEARCH</span>
          <span className={inputClass(filter.search !== '')}>
            <input
              type="search"
              value={filter.search}
              placeholder="LOCATION"
              onChange={(e) => set('search', e.target.value)}
            />
          </span>
        </label>

        <label className="filter-row">
          <span className="filter-label">METRIC</span>
          <span className="filter-input filter-input-active">
            <select
              value={filter.metric}
              onChange={(e) => set('metric', e.target.value as ReadingMetric)}
            >
              {METRICS.map((metric) => (
                <option key={metric.value} value={metric.value}>
                  {metric.label}
                </option>
              ))}
            </select>
          </span>
        </label>

        {showPageSize && onPageSize && (
          <label className="filter-row">
            <span className="filter-label">PAGE SIZE</span>
            <span className="filter-input">
              <select value={pageSize} onChange={(e) => onPageSize(Number(e.target.value))}>
                {PAGE_SIZES.map((size) => (
                  <option key={size} value={size}>
                    {size}
                  </option>
                ))}
              </select>
            </span>
          </label>
        )}

        <div className="segmented" role="group" aria-label="Aggregation interval">
          {INTERVALS.map((interval) => (
            <button
              type="button"
              key={interval}
              className={
                filter.interval === interval
                  ? 'segmented-option segmented-option-active'
                  : 'segmented-option'
              }
              aria-pressed={filter.interval === interval}
              onClick={() => set('interval', interval)}
            >
              {interval}
            </button>
          ))}
        </div>
      </div>

      {active.length > 0 && (
        <div className="filter-chips">
          {active.map((chip) => (
            <span key={chip.key} className="filter-chip">
              {chip.label}
              <button
                type="button"
                className="filter-chip-remove"
                aria-label={`Remove ${chip.label}`}
                onClick={() => set(chip.key, (chip.key === 'search' ? '' : null) as never)}
              >
                &#10005;
              </button>
            </span>
          ))}
        </div>
      )}

      <div className="pager">
        <span className="pager-note">
          {active.length === 0 ? 'NO FILTER APPLIED' : `${active.length} FILTERS ACTIVE`}
        </span>
        <button type="button" className="button button-ghost" onClick={clear}>
          CLEAR ALL
        </button>
      </div>
    </Window>
  );
}
