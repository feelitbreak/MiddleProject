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
      className="area-arrivals"
      query={
        lastEvent === null
          ? 'waiting for the first event…'
          : `${lastEvent.readingCount} rows · ${lastEvent.sensors.length} sensors · ${formatAge(lastEvent.publishedAt, now)} ago`
      }
    >
      <div className="scroll-area">
        {lastEvent === null ? (
          <div className="arrival">
            <span className="arrival-location">NO EVENTS YET</span>
            <span className="arrival-age">HUB IDLE</span>
          </div>
        ) : (
          lastEvent.sensors.map((sensor, index) => (
            <div
              className={index < 2 ? 'arrival arrival-fresh' : 'arrival'}
              key={`${sensor.location}-${sensor.sensorType}`}
            >
              <span className="arrival-location">{sensor.location.toUpperCase()}</span>
              <span className="tabular">{TYPE_LABEL[sensor.sensorType] ?? sensor.sensorType}</span>
              <span className="arrival-age">{formatAge(lastEvent.publishedAt, now)}</span>
            </div>
          ))
        )}
        {eventsSeen > 0 && (
          <div className="arrival">
            <span className="arrival-location">EVENTS THIS SESSION</span>
            <span className="tabular">{eventsSeen}</span>
          </div>
        )}
      </div>

      {lost.map((row) => (
        <div className="message message-error" key={row.location}>
          <div className="message-heading">
            &#9632; SIGNAL LOST &middot; {row.location.toUpperCase()}
          </div>
          <div className="message-body">
            Nothing reported for{' '}
            {row.oldestCollectedAt === null ? 'a while' : formatAge(row.oldestCollectedAt, now)}.
            Showing no value rather than a stale one.
          </div>
        </div>
      ))}
    </Window>
  );
}
