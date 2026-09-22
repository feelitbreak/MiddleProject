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

function hubDotClass(state: HubState): string {
  if (state === 'connected') return 'status-dot';
  if (state === 'disconnected') return 'status-dot status-dot-bad';
  return 'status-dot status-dot-warning';
}

function gatewayDotClass(status: HealthStatus | null): string {
  if (status === 'HEALTHY') return 'status-dot';
  if (status === 'UNHEALTHY' || status === null) return 'status-dot status-dot-bad';
  return 'status-dot status-dot-warning';
}

export default function MenuBar({ hubState, gatewayStatus, clock }: Readonly<MenuBarProps>) {
  return (
    <nav className="menubar">
      <b className="menubar-item menubar-logo">METER READINGS</b>
      <NavLink to="/" end>
        {({ isActive }) => (
          <b className="menubar-item menubar-route" aria-current={isActive ? 'page' : undefined}>
            CONSOLE
          </b>
        )}
      </NavLink>
      <NavLink to="/readings">
        {({ isActive }) => (
          <b className="menubar-item menubar-route" aria-current={isActive ? 'page' : undefined}>
            EXPLORER
          </b>
        )}
      </NavLink>
      <span className="menubar-spacer" />
      <span className="status-group">
        <span className="status-item">
          <span className={hubDotClass(hubState)} aria-hidden="true" />
          {HUB_LABEL[hubState]}
        </span>
        <span className="status-item">
          <span className={gatewayDotClass(gatewayStatus)} aria-hidden="true" />
          GATEWAY {gatewayStatus ?? 'UNKNOWN'}
        </span>
        <span className="menubar-clock tabular">{clock}</span>
      </span>
    </nav>
  );
}
