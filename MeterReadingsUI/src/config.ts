/**
 * Endpoint configuration, with same-origin paths as the default so the dev proxy and nginx can
 * forward them. Vite inlines these at build time, so a container cannot be repointed without a
 * rebuild -- change nginx.conf instead.
 */
export const GRAPHQL_URL = import.meta.env.VITE_GRAPHQL_URL ?? '/graphql';
export const HUB_URL = import.meta.env.VITE_HUB_URL ?? '/hubs/readings';
