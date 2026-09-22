/**
 * Endpoint configuration, with same-origin paths as the default so the dev proxy and nginx can
 * forward them. Vite inlines these at build time, so a container cannot be repointed without a
 * rebuild -- change nginx.conf instead.
 */

// Vite injects `import.meta.env`; under Jest there is no injection, so the defaults below apply.
const env: Partial<ImportMetaEnv> = import.meta.env ?? {};

export const GRAPHQL_URL = env.VITE_GRAPHQL_URL ?? '/graphql';
export const HUB_URL = env.VITE_HUB_URL ?? '/hubs/readings';
