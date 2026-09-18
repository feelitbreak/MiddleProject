import { NavLink } from 'react-router-dom';
import type { HubState } from '../../hooks/useLiveReadings';
import type { HealthStatus } from '../../graphql/generated/graphql';

interface MenuBarProps {
  hubState: HubState;
  gatewayStatus: HealthStatus | null;
  clock: string;
}

const HUB_LABEL: Record<HubState, string> = {
  connecting: 'HUB CONNECTING',
  connected: 'HUB CONNECTED',
  reconnecting: 'HUB RECONNECTING',
  disconnected: 'HUB OFFLINE',
};

function hubDot(state: HubState): string {
  if (state === 'connected') return 'd';
  if (state === 'disconnected') return 'd bad';
  return 'd warn';
}

function gatewayDot(status: HealthStatus | null): string {
  if (status === 'HEALTHY') return 'd';
  if (status === 'UNHEALTHY' || status === null) return 'd bad';
  return 'd warn';
}

export default function MenuBar({ hubState, gatewayStatus, clock }: MenuBarProps) {
  return (
    <nav className="menubar">
      <b className="logo">METER READINGS</b>
      <NavLink to="/" end>
        {({ isActive }) => (
          <b className="route" aria-current={isActive ? 'page' : undefined}>
            CONSOLE
          </b>
        )}
      </NavLink>
      <NavLink to="/readings">
        {({ isActive }) => (
          <b className="route" aria-current={isActive ? 'page' : undefined}>
            EXPLORER
          </b>
        )}
      </NavLink>
      <span className="grow" />
      <span className="strip">
        <span className="st">
          <span className={hubDot(hubState)} aria-hidden="true" />
          {HUB_LABEL[hubState]}
        </span>
        <span className="st">
          <span className={gatewayDot(gatewayStatus)} aria-hidden="true" />
          GATEWAY {gatewayStatus ?? 'UNKNOWN'}
        </span>
        <span className="clock num">{clock}</span>
      </span>
    </nav>
  );
}
