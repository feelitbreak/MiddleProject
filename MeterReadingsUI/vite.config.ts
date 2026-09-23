import react from '@vitejs/plugin-react';
import { defineConfig, loadEnv } from 'vite';

const DEFAULT_GRAPHQL_TARGET = 'http://localhost:8086';
const DEFAULT_HUB_TARGET = 'http://localhost:8088';

export default defineConfig(({ mode }) => {
  // Empty prefix so the proxy targets are readable here without being exposed to the bundle;
  // only VITE_-prefixed values reach client code.
  const env = loadEnv(mode, process.cwd(), '');

  return {
    plugins: [react()],
    server: {
      port: 5180,
      open: true,
      // The app calls same-origin paths and these forward them, so there is no CORS in development
      // and no back-end host in the bundle. nginx.conf mirrors both rules for the image.
      proxy: {
        '/graphql': {
          target: env['GRAPHQL_PROXY_TARGET'] ?? DEFAULT_GRAPHQL_TARGET,
          changeOrigin: true,
          headers: { 'X-Api-Key': env['GRAPHQL_API_KEY'] ?? '' },
        },
        '/hubs': {
          target: env['HUB_PROXY_TARGET'] ?? DEFAULT_HUB_TARGET,
          changeOrigin: true,
          ws: true,
          headers: { 'X-Api-Key': env['NOTIFICATION_API_KEY'] ?? '' },
        },
      },
    },
    build: {
      rollupOptions: {
        output: {
          // Dependencies change far less often than the app, so they cache separately. Rolldown
          // takes only the function form here.
          manualChunks(id: string) {
            if (!id.includes('node_modules')) return undefined;
            if (id.includes('@microsoft/signalr')) return 'signalr';
            if (id.includes('@apollo') || id.includes('graphql')) return 'apollo';
            return 'vendor';
          },
        },
      },
    },
  };
});
