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

/** Datetime-local wants `YYYY-MM-DDTHH:mm`; the gateway wants ISO with an offset. */
function toLocalInput(iso: string | null): string {
  if (iso === null) return '';
  const date = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
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
    <Window title="FILTER" tone="filter" className="a-filter">
      <div className="fgrid">
        <label className="fl">
          <span className="k">LOCATION</span>
          <span className={filter.location ? 'inp on' : 'inp'}>
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

        <label className="fl">
          <span className="k">TYPE</span>
          <span className={filter.sensorType ? 'inp on' : 'inp'}>
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

        <label className="fl">
          <span className="k">FROM</span>
          <span className="inp">
            <input
              type="datetime-local"
              value={toLocalInput(filter.from)}
              onChange={(e) =>
                set('from', e.target.value === '' ? null : new Date(e.target.value).toISOString())
              }
            />
          </span>
        </label>

        <label className="fl">
          <span className="k">TO</span>
          <span className={filter.to ? 'inp on' : 'inp'}>
            <input
              type="datetime-local"
              placeholder="NOW"
              value={toLocalInput(filter.to)}
              onChange={(e) =>
                set('to', e.target.value === '' ? null : new Date(e.target.value).toISOString())
              }
            />
          </span>
        </label>

        <label className="fl">
          <span className="k">SEARCH</span>
          <span className={filter.search ? 'inp on' : 'inp'}>
            <input
              type="search"
              value={filter.search}
              placeholder="LOCATION"
              onChange={(e) => set('search', e.target.value)}
            />
          </span>
        </label>

        <label className="fl">
          <span className="k">METRIC</span>
          <span className="inp on">
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
          <label className="fl">
            <span className="k">PAGE SIZE</span>
            <span className="inp">
              <select value={pageSize} onChange={(e) => onPageSize(Number(e.target.value))}>
                {[25, 50, 100].map((size) => (
                  <option key={size} value={size}>
                    {size}
                  </option>
                ))}
              </select>
            </span>
          </label>
        )}

        <div className="segs" role="group" aria-label="Aggregation interval">
          {INTERVALS.map((interval) => (
            <b
              key={interval}
              className={filter.interval === interval ? 'on' : undefined}
              role="button"
              tabIndex={0}
              onClick={() => set('interval', interval)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') set('interval', interval);
              }}
            >
              {interval}
            </b>
          ))}
        </div>
      </div>

      {active.length > 0 && (
        <div className="chips">
          {active.map((chip) => (
            <span key={chip.key} className="chip">
              {chip.label}
              <button
                type="button"
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
        <span className="c">
          {active.length === 0 ? 'NO FILTER APPLIED' : `${active.length} FILTERS ACTIVE`}
        </span>
        <button type="button" className="btn ghost" onClick={clear}>
          CLEAR ALL
        </button>
      </div>
    </Window>
  );
}
