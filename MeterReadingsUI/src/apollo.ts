import { ApolloClient, HttpLink, InMemoryCache, from } from '@apollo/client';
import { RetryLink } from '@apollo/client/link/retry';
import { relayStylePagination } from '@apollo/client/utilities';
import { GRAPHQL_URL } from './config';

/** Network failures only: a GraphQL error or a 4xx would just repeat, and a retried 429 is a storm. */
const retryLink = new RetryLink({
  delay: { initial: 300, max: 3000, jitter: true },
  attempts: {
    // Counts the first request, so this is two retries.
    max: 3,
    retryIf: (error: unknown) => {
      const status =
        typeof error === 'object' && error !== null && 'statusCode' in error
          ? error.statusCode
          : undefined;
      return typeof status !== 'number' || status >= 500;
    },
  },
});

export const apolloClient = new ApolloClient({
  link: from([retryLink, new HttpLink({ uri: GRAPHQL_URL })]),
  cache: new InMemoryCache({
    typePolicies: {
      Query: {
        fields: {
          // Keyed by the filter so switching filters starts a fresh page rather than appending to
          // the previous one's cursor chain.
          readings: relayStylePagination(['where']),
        },
      },
    },
  }),
  defaultOptions: {
    watchQuery: {
      // Keep the previous values on screen while a refetch is in flight; a blanked panel reads as
      // lost data on a feed that is legitimately slow.
      notifyOnNetworkStatusChange: true,
    },
  },
});
