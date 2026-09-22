import { MockedProvider, type MockedResponse } from '@apollo/client/testing';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { GraphQLError } from 'graphql';
import ExplorerPage from './ExplorerPage';
import { CATALOGUE, READINGS_PAGE } from '../../graphql/documents';
import { useFilter } from '../../hooks/useFilter';

/** The default filter carries a generated `from`, so mocks match on shape rather than exact values. */
function anyVariables() {
  return () => true;
}

const catalogueMock: MockedResponse = {
  request: { query: CATALOGUE },
  variableMatcher: anyVariables(),
  result: {
    data: {
      locations: ['Bedroom', 'Kitchen'],
      sensors: [{ __typename: 'Sensor', id: 1, name: 'Kitchen', type: 'AIR_QUALITY' }],
    },
  },
  maxUsageCount: 10,
};

function reading(id: number, co2: number | null = 500) {
  return {
    __typename: 'Reading',
    id,
    collectedAt: '2026-09-17T12:00:00.000Z',
    co2,
    pm25: 10,
    humidity: 45,
    motionDetected: null,
    energyKwh: null,
    sensor: { __typename: 'Sensor', id: 1, name: 'Kitchen', type: 'AIR_QUALITY' },
  };
}

function readingsMock(
  nodes: ReturnType<typeof reading>[],
  { totalCount = nodes.length, hasNextPage = false, endCursor = 'cursor-1' } = {},
): MockedResponse {
  return {
    request: { query: READINGS_PAGE },
    variableMatcher: anyVariables(),
    result: {
      data: {
        readings: {
          __typename: 'ReadingConnection',
          totalCount,
          pageInfo: { __typename: 'PageInfo', hasNextPage, endCursor },
          nodes,
        },
      },
    },
  };
}

function Harness() {
  const controls = useFilter();
  return <ExplorerPage controls={controls} />;
}

function renderExplorer(mocks: MockedResponse[]) {
  return render(
    <MockedProvider mocks={mocks}>
      <Harness />
    </MockedProvider>,
  );
}

describe('ExplorerPage', () => {
  it('should show the total count of matching readings, not just the page size', async () => {
    renderExplorer([catalogueMock, readingsMock([reading(1), reading(2)], { totalCount: 4107 })]);

    expect(await screen.findByText('4,107')).toBeInTheDocument();
    expect(screen.getByText(/SHOWING 2 OF 4,107/)).toBeInTheDocument();
  });

  it('should state that the ordering is fixed server-side', async () => {
    renderExplorer([catalogueMock, readingsMock([reading(1)])]);

    expect(await screen.findByText(/ORDER NEWEST FIRST \(FIXED SERVER-SIDE\)/)).toBeInTheDocument();
  });

  it('should disable load-more when the connection has no further page', async () => {
    renderExplorer([catalogueMock, readingsMock([reading(1)], { hasNextPage: false })]);

    const button = await screen.findByRole('button', { name: /LOAD 25 MORE/ });
    expect(button).toBeDisabled();
  });

  it('should enable load-more while the connection reports another page', async () => {
    renderExplorer([catalogueMock, readingsMock([reading(1)], { hasNextPage: true })]);

    await waitFor(() => expect(screen.getByRole('button', { name: /LOAD 25 MORE/ })).toBeEnabled());
  });

  it('should explain an empty result and offer a way out', async () => {
    renderExplorer([catalogueMock, readingsMock([], { totalCount: 0 })]);

    const empty = (await screen.findByText('NO READINGS IN RANGE')).closest('.empty-state');
    expect(empty).not.toBeNull();
    expect(
      within(empty as HTMLElement).getByRole('button', { name: 'CLEAR ALL' }),
    ).toBeInTheDocument();
  });

  it('should surface the gateway error code rather than an empty table', async () => {
    renderExplorer([
      catalogueMock,
      {
        request: { query: READINGS_PAGE },
        variableMatcher: anyVariables(),
        result: {
          errors: [new GraphQLError('boom', { extensions: { code: 'SERVICE_UNAVAILABLE' } })],
        },
      },
    ]);

    expect(await screen.findByText('SERVICE_UNAVAILABLE')).toBeInTheDocument();
  });

  it('should tint an out-of-band reading in the table', async () => {
    renderExplorer([catalogueMock, readingsMock([reading(1, 1200)])]);

    const value = await screen.findByText('1200');
    expect(value).toHaveClass('out-of-band');
  });

  it('should print the thresholds it judges the table against', async () => {
    renderExplorer([catalogueMock, readingsMock([reading(1)])]);

    expect(await screen.findByText(/OURS, NOT THE GATEWAY/i)).toBeInTheDocument();
  });

  it('should show identity columns while no single location is filtered', async () => {
    renderExplorer([catalogueMock, readingsMock([reading(1)])]);

    expect(await screen.findByRole('columnheader', { name: 'LOCATION' })).toBeInTheDocument();
    expect(screen.queryByText(/columns collapsed/)).not.toBeInTheDocument();
  });

  it('should collapse identity columns into a banner once one location is filtered', async () => {
    renderExplorer([catalogueMock, readingsMock([reading(1)]), readingsMock([reading(1)])]);
    await screen.findByRole('columnheader', { name: 'LOCATION' });

    await userEvent.selectOptions(screen.getByLabelText('LOCATION'), 'Kitchen');

    await waitFor(() => expect(screen.getByText(/columns collapsed/)).toBeInTheDocument());
    expect(screen.queryByRole('columnheader', { name: 'LOCATION' })).not.toBeInTheDocument();
  });
});
