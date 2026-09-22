import { Fragment } from 'react';

export interface ChartSeries {
  readonly name: string;
  readonly colour: string;
  /** null marks a period the series has no data for; the gap is drawn, never interpolated. */
  readonly values: readonly (number | null)[];
  /** Clock time of the last period that reported, labelling the gap. */
  readonly lostAt?: string;
}

interface SteppedChartProps {
  series: readonly ChartSeries[];
  ticks: readonly string[];
  unit: string;
  threshold?: { value: number; label: string } | null;
}

const WIDTH = 1100;
const HEIGHT = 168;
const PAD = { left: 52, right: 10, top: 10, bottom: 22 } as const;
const GRID_LINES = 3;

function niceBounds(series: readonly ChartSeries[]): { min: number; max: number } {
  const values = series.flatMap((s) => s.values).filter((v): v is number => v !== null);
  if (values.length === 0) return { min: 0, max: 1 };
  const low = Math.min(...values);
  const high = Math.max(...values);
  if (low === high) return { min: low - 1, max: high + 1 };
  // A floor of zero flattens same-unit series against the axis; pad the real range instead.
  const padding = (high - low) * 0.15;
  return { min: Math.floor(low - padding), max: Math.ceil(high + padding) };
}

/**
 * Drawn by hand rather than with a charting library: the design calls for mitred steps, square
 * markers and a hard outline, which is most of a library's defaults overridden anyway.
 */
export default function SteppedChart({
  series,
  ticks,
  unit,
  threshold,
}: Readonly<SteppedChartProps>) {
  const periods = series[0]?.values.length ?? 0;
  if (periods < 2) {
    return (
      <div className="empty-state">
        <span className="empty-title">NOT ENOUGH PERIODS</span>
        <span className="empty-detail">
          This range covers fewer than two periods. Widen it or pick a shorter interval.
        </span>
      </div>
    );
  }

  const { min, max } = niceBounds(series);
  const x = (i: number) => PAD.left + i * ((WIDTH - PAD.left - PAD.right) / (periods - 1));
  const y = (v: number) =>
    HEIGHT - PAD.bottom - ((v - min) / (max - min)) * (HEIGHT - PAD.top - PAD.bottom);

  return (
    <svg
      className="chart"
      viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
      preserveAspectRatio="none"
      role="img"
      aria-label={`Stepped chart, one series per location, in ${unit}`}
    >
      <defs>
        <pattern
          id="lostHatch"
          width="6"
          height="6"
          patternUnits="userSpaceOnUse"
          patternTransform="rotate(45)"
        >
          <rect width="6" height="6" fill="#FFE3EA" />
          <line x1="0" y1="0" x2="0" y2="6" stroke="#B3234C" strokeWidth="2.4" />
        </pattern>
      </defs>

      {Array.from({ length: GRID_LINES + 1 }, (_, g) => {
        const value = min + ((max - min) / GRID_LINES) * g;
        return (
          <Fragment key={`grid-${g}`}>
            <line
              x1={PAD.left}
              x2={WIDTH - PAD.right}
              y1={y(value)}
              y2={y(value)}
              stroke="#2A2140"
              strokeOpacity={g === 0 ? 0.5 : 0.13}
              strokeWidth={2}
            />
            <text
              x={4}
              y={y(value) + 5}
              fill="#6E6485"
              fontFamily="Handjet"
              fontSize={13}
              fontWeight={600}
            >
              {Math.round(value)}
            </text>
          </Fragment>
        );
      })}

      {/* Gaps are drawn behind the data: a lost feed must not dim the series that survived. */}
      {series.map((s) => {
        const gap = s.values.indexOf(null);
        if (gap <= 0) return null;
        return (
          <Fragment key={`gap-${s.name}`}>
            <rect
              x={x(gap - 1)}
              y={HEIGHT - PAD.bottom - 7}
              width={WIDTH - PAD.right - x(gap - 1)}
              height={7}
              fill="url(#lostHatch)"
            />
            {s.lostAt !== undefined && (
              <text
                x={x(gap - 1) + 8}
                y={HEIGHT - PAD.bottom - 12}
                fill="#B3234C"
                fontFamily="Handjet"
                fontSize={13}
                fontWeight={600}
              >
                {`${s.name.toUpperCase()} - NO DATA SINCE ${s.lostAt}`}
              </text>
            )}
          </Fragment>
        );
      })}

      {series.map((s) => {
        const points: string[] = [];
        s.values.forEach((v, i) => {
          if (v === null) return;
          const previous = i > 0 ? s.values[i - 1] : undefined;
          if (i > 0 && previous != null) points.push(`${x(i)},${y(previous)}`);
          points.push(`${x(i)},${y(v)}`);
        });
        if (points.length === 0) return null;
        const path = points.join(' ');
        return (
          <Fragment key={s.name}>
            <polyline
              points={path}
              fill="none"
              stroke="#2A2140"
              strokeWidth={5}
              strokeLinejoin="miter"
            />
            <polyline
              points={path}
              fill="none"
              stroke={s.colour}
              strokeWidth={2.6}
              strokeLinejoin="miter"
            />
            {s.values.map((v, i) =>
              v === null ? null : (
                <rect
                  key={`${s.name}-${i}`}
                  x={x(i) - 3}
                  y={y(v) - 3}
                  width={6}
                  height={6}
                  fill={s.colour}
                  stroke="#2A2140"
                  strokeWidth={1.8}
                />
              ),
            )}
          </Fragment>
        );
      })}

      {threshold && threshold.value >= min && threshold.value <= max && (
        <>
          <line
            x1={PAD.left}
            x2={WIDTH - PAD.right}
            y1={y(threshold.value)}
            y2={y(threshold.value)}
            stroke="#B3234C"
            strokeWidth={2}
            strokeDasharray="8 5"
          />
          <text
            x={PAD.left + 6}
            y={y(threshold.value) - 6}
            fill="#B3234C"
            fontFamily="Handjet"
            fontSize={13}
            fontWeight={600}
          >
            {threshold.label}
          </text>
        </>
      )}

      {ticks.map((label, i) => (
        <text
          key={label + String(i)}
          x={
            x(i * ((periods - 1) / Math.max(1, ticks.length - 1))) -
            (i === ticks.length - 1 ? 30 : 0)
          }
          y={HEIGHT - 4}
          fill="#6E6485"
          fontFamily="Handjet"
          fontSize={13}
          fontWeight={600}
        >
          {label}
        </text>
      ))}
    </svg>
  );
}

export function ChartLegend({
  series,
  note,
}: Readonly<{ series: readonly ChartSeries[]; note?: string }>) {
  return (
    <div className="chart-legend">
      {series.map((s) => (
        <span className="chart-legend-item" key={s.name}>
          <i className="chart-legend-swatch" style={{ background: s.colour }} />
          {s.name.toUpperCase()}
        </span>
      ))}
      {note !== undefined && <span className="chart-legend-note">{note}</span>}
    </div>
  );
}
