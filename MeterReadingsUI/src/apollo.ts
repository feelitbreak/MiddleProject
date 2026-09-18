import { ApolloClient, HttpLink, InMemoryCache } from '@apollo/client';
import { relayStylePagination } from '@apollo/client/utilities';

/**
 * Relative path on purpose: Vite proxies it in development and nginx proxies it in the image, so no
 * gateway host is baked into the bundle and the browser never makes a cross-origin request.
 */
const GRAPHQL_PATH = '/graphql';

export const apolloClient = new ApolloClient({
  link: new HttpLink({ uri: GRAPHQL_PATH }),
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
