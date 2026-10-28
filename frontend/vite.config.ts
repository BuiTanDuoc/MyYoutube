import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

// In development the browser talks to Vite (http://localhost:5173) and Vite forwards /api and /media
// to the .NET API. That avoids CORS and the self-signed HTTPS certificate warning.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const target = env.VITE_API_PROXY || 'https://localhost:7100';

  return {
    plugins: [react()],
    // hls.js alone is ~500 kB; it is split into the watch-page chunk and only loaded there
    build: { chunkSizeWarningLimit: 700 },
    server: {
      port: 5173,
      proxy: {
        '/api': { target, changeOrigin: true, secure: false },
        '/media': { target, changeOrigin: true, secure: false }
      }
    }
  };
});
