import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// Both back ends are proxied under this origin so the app only ever uses relative paths: no CORS
// in dev, no API host baked into the production bundle. nginx.conf mirrors these two rules.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5180,
    proxy: {
      '/graphql': { target: 'http://localhost:8086', changeOrigin: true },
      '/hubs': { target: 'http://localhost:8088', changeOrigin: true, ws: true },
    },
  },
});
