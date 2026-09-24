import { MockedProvider, type MockedResponse } from '@apollo/client/testing';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { GraphQLError } from 'graphql';
import { MemoryRouter } from 'react-router-dom';
import ConsolePage from './ConsolePage';
import { CATALOGUE, LATEST_READINGS, READING_AGGREGATES } from '../../graphql/documents';
import { useFilter } from '../../hooks/useFilter';
import type { LiveReadings } from '../../hooks/useLiveReadings';

const anyVariables = () => () => true;

const LIVE: LiveReadings = { hubState: 'connected', lastEvent: null, eventsSeen: 0 };

const catalogueMock: MockedResponse = {
  request: { query: CATALOGUE },
  variableMatcher: anyVariables(),
  result: {
    data: {
      locations: ['Kitchen', 'Office'],
      sensors: [
        { __typename: 'Sensor', id: 1, name: 'Kitchen', type: 'AIR_QUALITY' },
        { __typename: 'Sensor', id: 2, name: 'Kitchen', type: 'MOTION' },
        { __typename: 'Sensor', id: 3, name: 'Kitchen', type: 'ENERGY' },
      ],
    },
  },
  maxUsageCount: 10,
};

function latestMock(co2 = 500): MockedResponse {
  return {
    request: { query: LATEST_READINGS },
    variableMatcher: anyVariables(),
    maxUsageCount: 10,
    result: {
      data: {
        latestReadings: [
          {
            __typename: 'Reading',
            id: 1,
            collectedAt: new Date().toISOString(),
            co2,
            pm25: 10,
            humidity: 45,
            motionDetected: null,
            energyKwh: null,
            sensor: { __typename: 'Sensor', id: 1, name: 'Kitchen', type: 'AIR_QUALITY' },
          },
        ],
      },
    },
  };
}

function aggregatesMock(points = 3): MockedResponse {
  return {
    request: { query: READING_AGGREGATES },
    variableMatcher: anyVariables(),
    maxUsageCount: 10,
    result: {
      data: {
        readingAggregates: [
          {
            __typename: 'AggregateSeries',
            location: 'Kitchen',
            unit: 'ppm',
            points: Array.from({ length: points }, (_, i) => ({
              __typename: 'AggregatePoint',
              periodStart: `2026-09-17T0${String(i + 1)}:00:00.000Z`,
              count: 10,
              average: 400 + i * 50,
              minimum: 380,
              maximum: 460,
            })),
          },
        ],
      },
    },
  };
}

function Harness() {
  const controls = useFilter();
  return <ConsolePage controls={controls} live={LIVE} />;
}

function renderConsole(mocks: MockedResponse[]) {
  return render(
    <MemoryRouter>
      <MockedProvider mocks={mocks}>
        <Harness />
      </MockedProvider>
    </MemoryRouter>,
  );
}

describe('ConsolePage', () => {
  it('should say it is loading, not that nothing matches, before the first values arrive', () => {
    renderConsole([catalogueMock, latestMock(), aggregatesMock()]);

    expect(screen.getByText('Fetching the latest reading from each sensor.')).toBeInTheDocument();
    expect(screen.queryByText('NO SENSORS MATCH')).not.toBeInTheDocument();
  });

  it('should say when the location list could not be loaded', async () => {
    renderConsole([
      { request: { query: CATALOGUE }, variableMatcher: anyVariables(), error: new Error('down') },
      latestMock(),
      aggregatesMock(),
    ]);

    expect(await screen.findByText(/Locations couldn.t be loaded/)).toBeInTheDocument();
  });

  it('should report the sensor count from the catalogue, not the grouped row count', async () => {
    renderConsole([catalogueMock, latestMock(), aggregatesMock()]);

    expect(await screen.findByText(/3 SENSORS IN 1 LOCATIONS/)).toBeInTheDocument();
  });

  it('should render the latest values and the aggregate chart together', async () => {
    renderConsole([catalogueMock, latestMock(), aggregatesMock()]);

    expect(await screen.findAllByText('500')).not.toHaveLength(0);
    expect(
      await screen.findByRole('img', { name: /one series per location/i }),
    ).toBeInTheDocument();
  });

  it('should offer every location the catalogue knows about as a filter', async () => {
    renderConsole([catalogueMock, latestMock(), aggregatesMock()]);

    const select = await screen.findByLabelText('LOCATION');
    expect(select).toHaveDisplayValue('ALL');
    expect(screen.getByRole('option', { name: 'Kitchen' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Office' })).toBeInTheDocument();
  });

  /** Search narrows what is already on screen; the gateway has no search argument. */
  it('should filter the visible locations by the search term without refetching', async () => {
    renderConsole([catalogueMock, latestMock(), aggregatesMock()]);
    await screen.findAllByText('500');

    await userEvent.type(screen.getByLabelText('SEARCH'), 'office');

    await waitFor(() => expect(screen.getByText('NO SENSORS MATCH')).toBeInTheDocument());
  });

  it('should keep the location when the search matches it', async () => {
    renderConsole([catalogueMock, latestMock(), aggregatesMock()]);
    await screen.findAllByText('500');

    await userEvent.type(screen.getByLabelText('SEARCH'), 'kit');

    expect(screen.queryByText('NO SENSORS MATCH')).not.toBeInTheDocument();
  });

  it('should show the gateway error code when latest values fail', async () => {
    renderConsole([
      catalogueMock,
      {
        request: { query: LATEST_READINGS },
        variableMatcher: anyVariables(),
        result: {
          errors: [new GraphQLError('down', { extensions: { code: 'SERVICE_UNAVAILABLE' } })],
        },
      },
      aggregatesMock(),
    ]);

    expect(await screen.findByText('SERVICE_UNAVAILABLE')).toBeInTheDocument();
  });

  it('should explain an empty aggregate rather than drawing an empty frame', async () => {
    renderConsole([
      catalogueMock,
      latestMock(),
      {
        request: { query: READING_AGGREGATES },
        variableMatcher: anyVariables(),
        result: { data: { readingAggregates: [] } },
      },
    ]);

    expect(await screen.findByText('NO PERIODS IN RANGE')).toBeInTheDocument();
  });

  it('should refuse to plot a range that covers a single period', async () => {
    renderConsole([catalogueMock, latestMock(), aggregatesMock(1)]);

    expect(await screen.findByText('NOT ENOUGH PERIODS')).toBeInTheDocument();
  });
});
