import { Link } from 'react-router-dom';
import Window from '../shared/Window';
import { BandChip, FeedChip, LoadBar, ThresholdStrip } from '../shared/Indicators';
import { EmptyPanel } from '../shared/StatePanels';
import { formatAge } from '../../domain/freshness';
import { typesPresent, type LocationRow } from '../../domain/locations';
import { breachesFor, isBreached } from '../../domain/thresholds';

interface LatestValuesProps {
  rows: readonly LocationRow[];
  sensorCount: number;
  refreshing: boolean;
  now: number;
  query: string;
}

function numberCell(value: number | null, breached: boolean, digits = 0) {
  if (value === null) return <td className="na">&mdash;</td>;
  return <td className={breached ? 'num hot' : 'num'}>{value.toFixed(digits)}</td>;
}

export default function LatestValues({
  rows,
  sensorCount,
  refreshing,
  now,
  query,
}: LatestValuesProps) {
  return (
    <Window title="LATEST VALUES" className="a-latest" query={query}>
      {refreshing && <LoadBar />}
      <ThresholdStrip />

      {rows.length === 0 ? (
        <EmptyPanel
          title="NO SENSORS MATCH"
          detail="Nothing in the catalogue matches this filter. Clear the location or sensor type to see everything."
        />
      ) : (
        <>
          <div className="scrolls wide-only">
            <table>
              <thead>
                <tr>
                  <th>LOCATION</th>
                  <th>
                    CO2<i>PPM</i>
                  </th>
                  <th>
                    PM2.5<i>UG/M3</i>
                  </th>
                  <th>
                    RH<i>%</i>
                  </th>
                  <th>
                    MOTION<i>STATE</i>
                  </th>
                  <th>
                    ENERGY<i>KWH</i>
                  </th>
                  <th>
                    LAST READ<i>FEED</i>
                  </th>
                  <th>
                    READING<i>BAND</i>
                  </th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => {
                  const air = row.airQuality;
                  // A lost sensor shows nothing rather than a value that stopped being true.
                  const airLive = air !== null && air.state !== 'lost';
                  const motionLive = row.motion !== null && row.motion.state !== 'lost';
                  const energyLive = row.energy !== null && row.energy.state !== 'lost';
                  const breaches = airLive && air ? breachesFor(air) : [];

                  return (
                    <tr key={row.location}>
                      <td className="loc">
                        {row.location.toUpperCase()}
                        <i>{typesPresent(row)}</i>
                      </td>
                      {airLive && air ? (
                        <>
                          {numberCell(air.co2, isBreached('co2', air.co2))}
                          {numberCell(air.pm25, isBreached('pm25', air.pm25))}
                          {numberCell(air.humidity, isBreached('humidity', air.humidity))}
                        </>
                      ) : (
                        <>
                          <td className="na">&mdash;</td>
                          <td className="na">&mdash;</td>
                          <td className="na">&mdash;</td>
                        </>
                      )}
                      {motionLive && row.motion ? (
                        <td>{row.motion.detected === true ? 'YES' : 'NO'}</td>
                      ) : (
                        <td className="na">&mdash;</td>
                      )}
                      {energyLive && row.energy ? (
                        numberCell(row.energy.kwh, false, 1)
                      ) : (
                        <td className="na">&mdash;</td>
                      )}
                      <td>
                        <FeedChip
                          state={row.worst}
                          age={
                            row.oldestCollectedAt === null
                              ? '--'
                              : formatAge(row.oldestCollectedAt, now)
                          }
                        />
                      </td>
                      <td>
                        <BandChip breaches={breaches} hasThreshold={airLive} />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          {/* Below 720px the six-column table cannot hold display numerals: one card per location. */}
          <div className="cards scrolls">
            {rows.map((row) => {
              const air = row.airQuality;
              const airLive = air !== null && air.state !== 'lost';
              const breaches = airLive && air ? breachesFor(air) : [];
              return (
                <div className="card" key={row.location}>
                  <div className="hd">
                    <span className="n">{row.location.toUpperCase()}</span>
                    <span className="sub">{typesPresent(row)}</span>
                  </div>
                  {airLive && air && (
                    <>
                      <div className="mv">
                        <span className="k">CO2 PPM</span>
                        <span className={isBreached('co2', air.co2) ? 'v num hot' : 'v num'}>
                          {air.co2 ?? '--'}
                        </span>
                      </div>
                      <div className="mv">
                        <span className="k">PM2.5</span>
                        <span className={isBreached('pm25', air.pm25) ? 'v num hot' : 'v num'}>
                          {air.pm25 ?? '--'}
                        </span>
                      </div>
                      <div className="mv">
                        <span className="k">RH %</span>
                        <span
                          className={isBreached('humidity', air.humidity) ? 'v num hot' : 'v num'}
                        >
                          {air.humidity ?? '--'}
                        </span>
                      </div>
                    </>
                  )}
                  {row.energy && row.energy.state !== 'lost' && (
                    <div className="mv">
                      <span className="k">ENERGY KWH</span>
                      <span className="v num">{row.energy.kwh?.toFixed(1) ?? '--'}</span>
                    </div>
                  )}
                  {row.motion && row.motion.state !== 'lost' && (
                    <div className="mv">
                      <span className="k">MOTION</span>
                      <span className="v">{row.motion.detected === true ? 'YES' : 'NO'}</span>
                    </div>
                  )}
                  <div className="flagline">
                    <FeedChip
                      state={row.worst}
                      age={
                        row.oldestCollectedAt === null
                          ? '--'
                          : formatAge(row.oldestCollectedAt, now)
                      }
                    />
                    <BandChip breaches={breaches} hasThreshold={airLive} />
                  </div>
                </div>
              );
            })}
          </div>
        </>
      )}

      <div className="pager">
        <span className="c">
          SENSOR CATALOGUE &middot; {sensorCount} SENSORS IN {rows.length} LOCATIONS
        </span>
        <Link to="/readings" className="btn ghost">
          OPEN IN EXPLORER &#9656;
        </Link>
      </div>
    </Window>
  );
}
