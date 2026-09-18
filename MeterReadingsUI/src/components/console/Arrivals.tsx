import Window from '../shared/Window';
import { formatAge } from '../../domain/freshness';
import type { LocationRow } from '../../domain/locations';
import type { ReadingsChanged } from '../../hooks/useLiveReadings';

interface ArrivalsProps {
  lastEvent: ReadingsChanged | null;
  eventsSeen: number;
  rows: readonly LocationRow[];
  now: number;
}

const TYPE_LABEL: Record<string, string> = {
  air_quality: 'AIR QUALITY',
  motion: 'MOTION',
  energy: 'ENERGY',
};

export default function Arrivals({ lastEvent, eventsSeen, rows, now }: ArrivalsProps) {
  const lost = rows.filter((row) => row.worst === 'lost');

  return (
    <Window
      title="ARRIVALS &middot; readingsChanged"
      className="a-arrivals"
      query={
        lastEvent === null
          ? 'waiting for the first event…'
          : `${lastEvent.readingCount} rows · ${lastEvent.sensors.length} sensors · ${formatAge(lastEvent.publishedAt, now)} ago`
      }
    >
      <div className="scrolls">
        {lastEvent === null ? (
          <div className="ar">
            <span className="w">NO EVENTS YET</span>
            <span className="tk">HUB IDLE</span>
          </div>
        ) : (
          lastEvent.sensors.map((sensor, index) => (
            <div
              className={index < 2 ? 'ar f' : 'ar'}
              key={`${sensor.location}-${sensor.sensorType}`}
            >
              <span className="w">{sensor.location.toUpperCase()}</span>
              <span className="num">{TYPE_LABEL[sensor.sensorType] ?? sensor.sensorType}</span>
              <span className="tk">{formatAge(lastEvent.publishedAt, now)}</span>
            </div>
          ))
        )}
        {eventsSeen > 0 && (
          <div className="ar">
            <span className="w">EVENTS THIS SESSION</span>
            <span className="num">{eventsSeen}</span>
          </div>
        )}
      </div>

      {lost.map((row) => (
        <div className="msg err" key={row.location}>
          <div className="h">&#9632; SIGNAL LOST &middot; {row.location.toUpperCase()}</div>
          <div className="m">
            Nothing reported for{' '}
            {row.oldestCollectedAt === null ? 'a while' : formatAge(row.oldestCollectedAt, now)}.
            Showing no value rather than a stale one.
          </div>
        </div>
      ))}
    </Window>
  );
}
