/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Where the browser sends GraphQL. A same-origin path by default, so the proxy handles it. */
  readonly VITE_GRAPHQL_URL?: string;
  /** Where the browser opens the SignalR connection. */
  readonly VITE_HUB_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
