import { jest } from '@jest/globals';
import { ApolloClient, ApolloProvider, InMemoryCache } from '@apollo/client';
import { MockLink } from '@apollo/client/testing';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import {
  CATALOGUE,
  GATEWAY_HEALTH,
  LATEST_READINGS,
  READINGS_PAGE,
  READING_AGGREGATES,
} from './graphql/documents';
import type { LiveReadings } from './hooks/useLiveReadings';

let onChanged: (() => void) | undefined;

jest.unstable_mockModule('./hooks/useLiveReadings', () => ({
  useLiveReadings: (callback: () => void): LiveReadings => {
    onChanged = callback;
    return { hubState: 'connected', lastEvent: null, eventsSeen: 0 };
  },
}));

const { default: App } = await import('./App');

function renderApp(path: string) {
  const client = new ApolloClient({
    link: new MockLink([], true, { showWarnings: false }),
    cache: new InMemoryCache(),
  });
  const refetch = jest
    .spyOn(client, 'refetchQueries')
    .mockReturnValue(Promise.resolve([]) as never);

  render(
    <MemoryRouter initialEntries={[path]}>
      <ApolloProvider client={client}>
        <App />
      </ApolloProvider>
    </MemoryRouter>,
  );

  return refetch;
}

describe('App', () => {
  it('should refetch what a hub event invalidates and leave the health poll alone', () => {
    const refetch = renderApp('/');

    onChanged?.();

    expect(refetch).toHaveBeenCalledWith({
      include: [CATALOGUE, LATEST_READINGS, READING_AGGREGATES, READINGS_PAGE],
    });
    expect(refetch.mock.calls[0]?.[0]?.include).not.toContain(GATEWAY_HEALTH);
  });

  it('should show a loading panel while the explorer screen downloads', async () => {
    renderApp('/readings');

    expect(screen.getByText('Loading the page.')).toBeInTheDocument();
    expect(await screen.findByText('MATCHING SET')).toBeInTheDocument();
  });
});
