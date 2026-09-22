import { Link } from 'react-router-dom';
import Window from '../shared/Window';
import { BandChip, FeedChip, RefreshBar, ThresholdStrip } from '../shared/Indicators';
import { EmptyPanel } from '../shared/StatePanels';
import { formatAge } from '../../domain/freshness';
import { typesPresent, type AirQualitySlot, type LocationRow } from '../../domain/locations';
import { breachesFor, isBreached } from '../../domain/thresholds';

interface LatestValuesProps {
  rows: readonly LocationRow[];
  sensorCount: number;
  refreshing: boolean;
  now: number;
  query: string;
}

function numberCell(value: number | null, breached: boolean, digits = 0) {
  if (value === null) return <td className="no-value">&mdash;</td>;
  return <td className={breached ? 'tabular out-of-band' : 'tabular'}>{value.toFixed(digits)}</td>;
}

/** A lost sensor shows nothing rather than a value that stopped being true. */
function reporting<T extends { state: string }>(slot: T | null): T | null {
  return slot !== null && slot.state !== 'lost' ? slot : null;
}

function metricValueClass(breached: boolean): string {
  return breached ? 'metric-value tabular out-of-band' : 'metric-value tabular';
}

export default function LatestValues({
  rows,
  sensorCount,
  refreshing,
  now,
  query,
}: Readonly<LatestValuesProps>) {
  const ageOf = (row: LocationRow) =>
    row.oldestCollectedAt === null ? '--' : formatAge(row.oldestCollectedAt, now);

  const breachesOf = (air: AirQualitySlot | null) => (air === null ? [] : breachesFor(air));

  return (
    <Window title="LATEST VALUES" className="area-latest" query={query}>
      {refreshing && <RefreshBar />}
      <ThresholdStrip />

      {rows.length === 0 ? (
        <EmptyPanel
          title="NO SENSORS MATCH"
          detail="Nothing in the catalogue matches this filter. Clear the location or sensor type to see everything."
        />
      ) : (
        <>
          <div className="scroll-area desktop-only">
            <table>
              <thead>
                <tr>
                  <th>LOCATION</th>
                  <th>
                    CO2<i className="column-unit">PPM</i>
                  </th>
                  <th>
                    PM2.5<i className="column-unit">UG/M3</i>
                  </th>
                  <th>
                    RH<i className="column-unit">%</i>
                  </th>
                  <th>
                    MOTION<i className="column-unit">STATE</i>
                  </th>
                  <th>
                    ENERGY<i className="column-unit">KWH</i>
                  </th>
                  <th>
                    LAST READ<i className="column-unit">FEED</i>
                  </th>
                  <th>
                    READING<i className="column-unit">BAND</i>
                  </th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => {
                  const air = reporting(row.airQuality);
                  const motion = reporting(row.motion);
                  const energy = reporting(row.energy);

                  return (
                    <tr key={row.location}>
                      <td className="location-cell">
                        {row.location.toUpperCase()}
                        <i className="location-types">{typesPresent(row)}</i>
                      </td>
                      {air === null ? (
                        <>
                          <td className="no-value">&mdash;</td>
                          <td className="no-value">&mdash;</td>
                          <td className="no-value">&mdash;</td>
                        </>
                      ) : (
                        <>
                          {numberCell(air.co2, isBreached('co2', air.co2))}
                          {numberCell(air.pm25, isBreached('pm25', air.pm25))}
                          {numberCell(air.humidity, isBreached('humidity', air.humidity))}
                        </>
                      )}
                      {motion === null ? (
                        <td className="no-value">&mdash;</td>
                      ) : (
                        <td>{motion.detected === true ? 'YES' : 'NO'}</td>
                      )}
                      {energy === null ? (
                        <td className="no-value">&mdash;</td>
                      ) : (
                        numberCell(energy.kwh, false, 1)
                      )}
                      <td>
                        <FeedChip state={row.worst} age={ageOf(row)} />
                      </td>
                      <td>
                        <BandChip breaches={breachesOf(air)} hasThreshold={air !== null} />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          {/* Below 720px the six-column table cannot hold display numerals: one card per location. */}
          <div className="location-cards scroll-area">
            {rows.map((row) => {
              const air = reporting(row.airQuality);
              const motion = reporting(row.motion);
              const energy = reporting(row.energy);

              return (
                <div className="location-card" key={row.location}>
                  <div className="card-header">
                    <span className="card-name">{row.location.toUpperCase()}</span>
                    <span className="card-types">{typesPresent(row)}</span>
                  </div>
                  {air !== null && (
                    <>
                      <div className="metric-tile">
                        <span className="metric-label">CO2 PPM</span>
                        <span className={metricValueClass(isBreached('co2', air.co2))}>
                          {air.co2 ?? '--'}
                        </span>
                      </div>
                      <div className="metric-tile">
                        <span className="metric-label">PM2.5</span>
                        <span className={metricValueClass(isBreached('pm25', air.pm25))}>
                          {air.pm25 ?? '--'}
                        </span>
                      </div>
                      <div className="metric-tile">
                        <span className="metric-label">RH %</span>
                        <span className={metricValueClass(isBreached('humidity', air.humidity))}>
                          {air.humidity ?? '--'}
                        </span>
                      </div>
                    </>
                  )}
                  {energy !== null && (
                    <div className="metric-tile">
                      <span className="metric-label">ENERGY KWH</span>
                      <span className="metric-value tabular">{energy.kwh?.toFixed(1) ?? '--'}</span>
                    </div>
                  )}
                  {motion !== null && (
                    <div className="metric-tile">
                      <span className="metric-label">MOTION</span>
                      <span className="metric-value">
                        {motion.detected === true ? 'YES' : 'NO'}
                      </span>
                    </div>
                  )}
                  <div className="card-flags">
                    <FeedChip state={row.worst} age={ageOf(row)} />
                    <BandChip breaches={breachesOf(air)} hasThreshold={air !== null} />
                  </div>
                </div>
              );
            })}
          </div>
        </>
      )}

      <div className="pager">
        <span className="pager-note">
          SENSOR CATALOGUE &middot; {sensorCount} SENSORS IN {rows.length} LOCATIONS
        </span>
        <Link to="/readings" className="button button-ghost">
          OPEN IN EXPLORER &#9656;
        </Link>
      </div>
    </Window>
  );
}
