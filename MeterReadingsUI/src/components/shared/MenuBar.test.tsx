import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import MenuBar from './MenuBar';
import type { HubState } from '../../hooks/useLiveReadings';
import type { HealthStatus } from '../../graphql/generated/graphql';

function renderBar(
  hubState: HubState = 'connected',
  gatewayStatus: HealthStatus | null = 'HEALTHY',
  route = '/',
) {
  return render(
    <MemoryRouter initialEntries={[route]}>
      <MenuBar hubState={hubState} gatewayStatus={gatewayStatus} clock="09:41:36" />
    </MemoryRouter>,
  );
}

describe('MenuBar', () => {
  it.each([
    ['connecting', 'HUB CONNECTING'],
    ['connected', 'HUB CONNECTED'],
    ['reconnecting', 'HUB RECONNECTING'],
    ['disconnected', 'HUB OFFLINE'],
  ] as const)('should name the %s hub state in words', (state: HubState, label) => {
    renderBar(state);
    expect(screen.getByText(label)).toBeInTheDocument();
  });

  it('should mark a healthy hub and gateway without a warning colour', () => {
    const { container } = renderBar('connected', 'HEALTHY');

    expect(container.querySelectorAll('.status-dot-bad')).toHaveLength(0);
    expect(container.querySelectorAll('.status-dot-warning')).toHaveLength(0);
  });

  it('should mark a reconnecting hub as a warning, not a failure', () => {
    const { container } = renderBar('reconnecting', 'HEALTHY');

    expect(container.querySelectorAll('.status-dot-warning')).toHaveLength(1);
    expect(container.querySelectorAll('.status-dot-bad')).toHaveLength(0);
  });

  it('should mark an offline hub as a failure', () => {
    const { container } = renderBar('disconnected', 'HEALTHY');
    expect(container.querySelectorAll('.status-dot-bad')).toHaveLength(1);
  });

  /** Not knowing the gateway's health is not the same as knowing it is fine. */
  it('should treat an unknown gateway status as a failure rather than assume health', () => {
    const { container } = renderBar('connected', null);

    expect(screen.getByText('GATEWAY UNKNOWN')).toBeInTheDocument();
    expect(container.querySelectorAll('.status-dot-bad')).toHaveLength(1);
  });

  it('should mark a degraded gateway as a warning', () => {
    const { container } = renderBar('connected', 'DEGRADED');

    expect(screen.getByText('GATEWAY DEGRADED')).toBeInTheDocument();
    expect(container.querySelectorAll('.status-dot-warning')).toHaveLength(1);
  });

  it('should mark the current route and only that one', () => {
    renderBar('connected', 'HEALTHY', '/readings');

    expect(screen.getByText('EXPLORER')).toHaveAttribute('aria-current', 'page');
    expect(screen.getByText('CONSOLE')).not.toHaveAttribute('aria-current');
  });

  it('should show the clock it was given', () => {
    renderBar();
    expect(screen.getByText('09:41:36')).toBeInTheDocument();
  });
});
