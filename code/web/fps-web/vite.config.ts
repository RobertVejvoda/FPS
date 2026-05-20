import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@fps/api-client': '../../clients/typescript/src',
    },
  },
  server: {
    port: 5200,
  },
});
